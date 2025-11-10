using System.Collections;
using UnityEngine;
using Cinemachine;

/// <summary>
/// 战斗摄像机控制器（Cinemachine 版）：
/// 使用 Cinemachine FreeLook 组件提供角色环绕与平滑，无需自定义 yaw/pitch 计算，降低抖动。
/// - freeLookCam.Follow / LookAt 指向一个平滑 Pivot（只跟随位置不继承旋转）。
/// - 切换单位时仅平滑移动 Pivot，保留当前 FreeLook 角度（可选）。
/// - 兼容旧接口：TransitionToTarget / FocusImmediate / FocusOn。
/// </summary>
public class BattleCameraController : MonoBehaviour
{
    [Header("Cinemachine Cameras")]
    public CinemachineVirtualCamera overviewCam; // 保留但战斗中不启用
    public CinemachineFreeLook freeLookCam;      // 主摄像机（需在场景中创建并指派）

    [Header("Follow Pivot")]
    [Tooltip("Pivot 平滑时间 (秒)，控制镜头跟随单位根的平滑程度")] public float pivotSmoothTime = 0.08f;
    [Tooltip("切换单位时的过渡时长 (秒)")] public float switchTransitionTime = 0.35f;
    [Tooltip("切换过渡曲线")]
    public AnimationCurve switchCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("切换时保持当前镜头角度 (X 轴/Y 轴) 不重置")] public bool preserveAnglesOnSwitch = true;

    [Header("默认椭球参数 (用于缺少 CameraOrbitRig 时自动生成)")]
    public Vector3 defaultRadii = new Vector3(3f, 2.0f, 3f);
    public Vector3 defaultCenterOffset = new Vector3(0f, 1.25f, 0f);

    [Header("FreeLook 参数 (仅在未使用输入系统时) ")] public bool lockXAxisWhenDisabled = true; public bool lockYAxisWhenDisabled = true;

    [Header("Mouse Axis Inversion (反转设置)")]
    [Tooltip("水平轴是否反转（Mouse X）")] public bool invertX = false;
    [Tooltip("垂直轴是否反转（Mouse Y）。大多数第三人称需要勾选，使鼠标上移视角下倾感觉正确")] public bool invertY = true;

    [Header("Scroll Zoom (鼠标滚轮缩放)")]
    [Tooltip("启用鼠标滚轮缩放 FreeLook 距离(缩放三个 Rig 的半径)")] public bool enableScrollZoom = true;
    [Tooltip("缩放灵敏度（每滚轮单位对缩放比例的影响）")] public float zoomSensitivity = 0.5f;
    [Tooltip("最小缩放比例（越小越近）")] public float minZoomScale = 0.5f;
    [Tooltip("最大缩放比例（越大越远）")] public float maxZoomScale = 1.8f;
    [Tooltip("缩放平滑时间（秒），0 表示无平滑")]
    public float zoomSmoothTime = 0.08f;

    [Header("Axis Sensitivity (旋转灵敏度)")]
    [Tooltip("水平旋转最大速度（数值越小越慢）")] public float xAxisMaxSpeed = 120f;
    [Tooltip("垂直旋转最大速度（数值越小越慢）")] public float yAxisMaxSpeed = 1.0f;

    [Header("Global View Init")]
    [Tooltip("启动时自动将摄像机定位到全局概览位置")] public bool autoGlobalViewOnAwake = true;
    [Tooltip("计算全局范围时的额外边距")] public float globalViewPadding = 2f;
    [Tooltip("Pivot 在全局视角时的额外高度")] public float globalViewHeight = 6f;
    [Tooltip("初始水平角度 (度)，用于全局视角")] public float globalViewYaw = 35f;
    [Tooltip("初始垂直插值 (0..1)，数值越大越俯视")] public float globalViewYAxisValue = 0.7f;

    [Header("No-Focus Behavior")]
    [Tooltip("当未聚焦任何单位时，禁用 Cinemachine 的自动回中以防止抖动/自动旋转")] public bool disableRecenteringWhenNoFocus = true;

    [Header("Turn Switch Focus")]
    [Tooltip("切换目标时是否强制立即聚焦（忽略平滑过渡）")] public bool forceImmediateOnTurnSwitch = true;
    [Tooltip("强制立即聚焦时的最大距离阈值，超出则不聚焦")] public float immediateSnapDistanceThreshold = 0.25f;

    // 内部状态
    private Transform _focusTarget;            // 当前单位根
    private CameraOrbitRig _currentRig;        // 当前单位椭球（仅用于中心偏移）
    private Transform _pivot;                  // 镜头跟随的独立平滑点
    private Vector3 _pivotVel;                 // 平滑速度
    private bool _inSwitch;                    // 是否在切换过渡中
    private float _switchElapsed;              // 过渡计时
    private Vector3 _switchStartPos;           // 过渡起点
    private Vector3 _switchEndPos;             // 过渡终点

    private bool _hasFocusedOnce = false;
    private bool _mouseControlEnabled = false;

    // 记录角度（在未使用输入系统或需要自定义锁定时使用）
    private float _cachedXAxis; // FreeLook m_XAxis.Value (0..360)
    private float _cachedYAxis; // FreeLook m_YAxis.Value (0..1)

    // 缩放：基准半径与运行时缩放
    private CinemachineFreeLook.Orbit[] _baseOrbits; // 长度=3
    private float _zoomTarget = 1f;
    private float _zoomCurrent = 1f;
    private float _zoomVel;

    private bool _noFocusSettingsApplied = false;

    void Awake()
    {
        // 确保主摄像机上存在 CinemachineBrain，才能让虚拟相机接管
        var mainCam = Camera.main;
        if (mainCam != null && mainCam.GetComponent<CinemachineBrain>() == null)
        {
            mainCam.gameObject.AddComponent<CinemachineBrain>();
        }

        EnsurePivot();
        if (freeLookCam != null)
        {
            freeLookCam.Follow = _pivot;
            freeLookCam.LookAt = _pivot; // 避免角色自身旋转造成抖动
            freeLookCam.Priority = 100;   // 提高优先级，确保成为激活虚拟相机
            freeLookCam.enabled = true;
            ApplyAxisSensitivity();
            ApplyAxisInversion();
            // 默认使用世界绑定，避免受目标朝向影响
            freeLookCam.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
            CacheBaseOrbits();
        }
        if (overviewCam != null) overviewCam.Priority = 0;

        if (autoGlobalViewOnAwake)
        {
            TryInitializeGlobalView();
        }

        // 初始为无焦点，应用防抖设置
        ApplyNoFocusSettings(_focusTarget == null);
    }

    void LateUpdate()
    {
        UpdatePivotMotion();
        MaintainAxisLockIfDisabled();
        UpdateScrollZoom();

        // 根据是否有焦点切换自动回中等设置（只在状态变更时应用一次）
        if (disableRecenteringWhenNoFocus)
        {
            if (_focusTarget == null && !_noFocusSettingsApplied) ApplyNoFocusSettings(true);
            else if (_focusTarget != null && _noFocusSettingsApplied) ApplyNoFocusSettings(false);
        }
    }

    private void UpdatePivotMotion()
    {
        if (_focusTarget == null) return;
        Vector3 desiredCenter = _focusTarget.position + (_currentRig != null ? _currentRig.centerOffset : Vector3.zero);

        if (_inSwitch)
        {
            _switchElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_switchElapsed / Mathf.Max(switchTransitionTime, 0.0001f));
            float w = switchCurve != null ? switchCurve.Evaluate(t) : t;
            _pivot.position = Vector3.Lerp(_switchStartPos, _switchEndPos, w);
            if (t >= 1f)
            {
                _inSwitch = false;
                _pivot.position = _switchEndPos;
            }
        }
        else
        {
            _pivot.position = Vector3.SmoothDamp(_pivot.position, desiredCenter, ref _pivotVel, pivotSmoothTime);
        }
    }

    private void MaintainAxisLockIfDisabled()
    {
        if (freeLookCam == null) return;
        if (!_mouseControlEnabled)
        {
            // 锁定当前值避免输入系统仍修改
            if (lockXAxisWhenDisabled)
            {
                freeLookCam.m_XAxis.m_InputAxisName = string.Empty;
                freeLookCam.m_XAxis.m_InputAxisValue = 0f;
                freeLookCam.m_XAxis.Value = _cachedXAxis;
            }
            if (lockYAxisWhenDisabled)
            {
                freeLookCam.m_YAxis.m_InputAxisName = string.Empty;
                freeLookCam.m_YAxis.m_InputAxisValue = 0f;
                freeLookCam.m_YAxis.Value = _cachedYAxis;
            }
        }
        else
        {
            // 启用输入：若未指定输入轴名则默认使用 Mouse X / Mouse Y
            if (string.IsNullOrEmpty(freeLookCam.m_XAxis.m_InputAxisName)) freeLookCam.m_XAxis.m_InputAxisName = "Mouse X";
            if (string.IsNullOrEmpty(freeLookCam.m_YAxis.m_InputAxisName)) freeLookCam.m_YAxis.m_InputAxisName = "Mouse Y";
            _cachedXAxis = freeLookCam.m_XAxis.Value;
            _cachedYAxis = freeLookCam.m_YAxis.Value;
        }
    }

    private void UpdateScrollZoom()
    {
        if (!enableScrollZoom || freeLookCam == null || _baseOrbits == null || _baseOrbits.Length != 3) return;
        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) > 0.0001f)
        {
            // 滚轮向前(正) -> 靠近：缩放减小
            _zoomTarget = Mathf.Clamp(_zoomTarget - wheel * zoomSensitivity, minZoomScale, maxZoomScale);
        }
        if (Mathf.Abs(_zoomCurrent - _zoomTarget) > 0.0001f)
        {
            if (zoomSmoothTime > 0f)
            {
                _zoomCurrent = Mathf.SmoothDamp(_zoomCurrent, _zoomTarget, ref _zoomVel, zoomSmoothTime);
            }
            else
            {
                _zoomCurrent = _zoomTarget;
            }
            ApplyZoomToOrbits(_zoomCurrent);
        }
    }

    private void ApplyZoomToOrbits(float scale)
    {
        if (freeLookCam == null || _baseOrbits == null || _baseOrbits.Length != 3) return;
        var orbits = freeLookCam.m_Orbits;
        for (int i = 0; i < orbits.Length && i < _baseOrbits.Length; i++)
        {
            orbits[i].m_Radius = Mathf.Max(0.1f, _baseOrbits[i].m_Radius * scale);
            // 保持高度不变，避免构图跳变；如需等比缩放可改为：orbits[i].m_Height = _baseOrbits[i].m_Height * scale;
        }
        freeLookCam.m_Orbits = orbits; // 赋值回去以生效
    }

    private void CacheBaseOrbits()
    {
        if (freeLookCam == null) return;
        var src = freeLookCam.m_Orbits;
        _baseOrbits = new CinemachineFreeLook.Orbit[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            _baseOrbits[i] = new CinemachineFreeLook.Orbit(src[i].m_Height, src[i].m_Radius);
        }
        _zoomTarget = _zoomCurrent = 1f;
    }

    // ============= 外部接口 =============

    public void EnableMouseControl(bool enable)
    {
        _mouseControlEnabled = enable;
        if (!enable && freeLookCam != null)
        {
            // 记录当前角度
            _cachedXAxis = freeLookCam.m_XAxis.Value;
            _cachedYAxis = freeLookCam.m_YAxis.Value;
        }
        // 每次开关时同步一次灵敏度/反转设置（以防 Inspector 改动在运行时生效）
        ApplyAxisSensitivity();
        ApplyAxisInversion();
        // 根据有无焦点应用/恢复自动回中
        ApplyNoFocusSettings(_focusTarget == null);
    }

    public void FocusImmediate(Transform target)
    {
        if (freeLookCam == null || target == null) return;
        _focusTarget = target;
        _currentRig = GetOrCreateRig(target);
        _pivot.position = target.position + _currentRig.centerOffset;
        _inSwitch = false;
        _switchElapsed = 0f;
        if (!_hasFocusedOnce)
        {
            InitializeDefaultAxes();
            _hasFocusedOnce = true;
        }
        else if (!preserveAnglesOnSwitch)
        {
            InitializeDefaultAxes();
        }
        // 切换单位时不重置缩放；如需重置可取消注释：
        // _zoomTarget = _zoomCurrent = 1f; ApplyZoomToOrbits(_zoomCurrent);
        ApplyAxisSensitivity();
        ApplyAxisInversion();
        ApplyNoFocusSettings(false);
    }

    public void FocusSmooth(Transform target)
    {
        if (freeLookCam == null || target == null) return;
        _currentRig = GetOrCreateRig(target);
        Vector3 endPos = target.position + _currentRig.centerOffset;

        if (_focusTarget == null)
        {
            _focusTarget = target;
            _pivot.position = endPos;
            InitializeDefaultAxes();
            _hasFocusedOnce = true;
            ApplyAxisSensitivity();
            ApplyAxisInversion();
            ApplyNoFocusSettings(false);
            return;
        }

        _focusTarget = target;
        _switchStartPos = _pivot.position;
        _switchEndPos = endPos;
        _switchElapsed = 0f;
        _inSwitch = true;
        if (!preserveAnglesOnSwitch) InitializeDefaultAxes();
        // _zoom 保持，避免切换时远近跳变
        ApplyAxisSensitivity();
        ApplyAxisInversion();
        ApplyNoFocusSettings(false);
    }

    public void OnTurnSwitch(Transform target)
    {
        if (target == null) return;
        if (forceImmediateOnTurnSwitch)
        {
            // 如果切换目标时与之前的目标距离很近，则强制立即聚焦
            if (_pivot != null)
            {
                Vector3 desired = target.position + (GetOrCreateRig(target)?.centerOffset ?? Vector3.zero);
                float dist = Vector3.Distance(_pivot.position, desired);
                if (dist <= immediateSnapDistanceThreshold)
                {
                    FocusImmediate(target); // 直接聚焦
                    return;
                }
            }
            FocusImmediate(target); // 否则强制执行聚焦
        }
        else
        {
            if (_focusTarget == null) FocusImmediate(target); else FocusSmooth(target);
        }
    }

    /// <summary>
    /// 强制立即聚焦目标（可选是否保留角度），不使用切换动画.
    /// </summary>
    public void ForceFocusTarget(Transform target, bool preserveAngles = true)
    {
        if (target == null || freeLookCam == null) return;
        preserveAnglesOnSwitch = preserveAngles;
        FocusImmediate(target);
    }

    public IEnumerator TransitionToTarget(Transform from, Transform to, Vector3 unusedOffset, float duration)
    {
        OnTurnSwitch(to);
        float wait = _inSwitch ? switchTransitionTime : 0f;
        yield return new WaitForSeconds(wait);
    }

    public IEnumerator FocusOn(Transform unit, Vector3 followOffset, float holdSeconds)
    {
        OnTurnSwitch(unit);
        yield return new WaitForSeconds(Mathf.Max(holdSeconds, switchTransitionTime));
    }

    public IEnumerator PlayActionCam(Transform actor, Transform target, Vector3 offset, float actionDuration)
    {
        // 已弃用: 不切换到独立演出相机，直接等待
        yield return new WaitForSeconds(actionDuration);
    }

    public void ShowOverview()
    {
        if (freeLookCam != null) freeLookCam.Priority = 100;
        if (overviewCam != null) overviewCam.Priority = 0;
        ApplyNoFocusSettings(true);
    }

    // ============= 辅助 =============

    private void InitializeDefaultAxes()
    {
        if (freeLookCam == null) return;
        // 默认设置：水平角度保持当前 (不重置为 0)，垂直角度置于中上位置 0.6
        if (!preserveAnglesOnSwitch || !_hasFocusedOnce)
        {
            freeLookCam.m_XAxis.Value = freeLookCam.m_XAxis.Value; // 保留现值
            freeLookCam.m_YAxis.Value = 0.6f;
        }
        _cachedXAxis = freeLookCam.m_XAxis.Value;
        _cachedYAxis = freeLookCam.m_YAxis.Value;
    }

    private void EnsurePivot()
    {
        if (_pivot != null) return;
        GameObject go = new GameObject("FreeLookPivot");
        go.hideFlags = HideFlags.DontSave;
        _pivot = go.transform;
        _pivot.position = transform.position;
    }

    private CameraOrbitRig GetOrCreateRig(Transform t)
    {
        var rig = t.GetComponent<CameraOrbitRig>();
        if (rig == null)
        {
            rig = t.gameObject.AddComponent<CameraOrbitRig>();
            rig.radii = defaultRadii;
            rig.centerOffset = defaultCenterOffset;
        }
        return rig;
    }

    private void ApplyAxisSensitivity()
    {
        if (freeLookCam == null) return;
        freeLookCam.m_XAxis.m_MaxSpeed = Mathf.Max(0f, xAxisMaxSpeed);
        freeLookCam.m_YAxis.m_MaxSpeed = Mathf.Max(0f, yAxisMaxSpeed);
    }

    private void ApplyAxisInversion()
    {
        if (freeLookCam == null) return;
        freeLookCam.m_XAxis.m_InvertInput = invertX;
        freeLookCam.m_YAxis.m_InvertInput = invertY;
    }

    private void ApplyNoFocusSettings(bool noFocus)
    {
        if (freeLookCam == null) return;
        if (!disableRecenteringWhenNoFocus)
        {
            _noFocusSettingsApplied = false;
            return;
        }
        if (noFocus)
        {
            // 关闭自动回中，固定于当前视角
            freeLookCam.m_RecenterToTargetHeading.m_enabled = false;
            freeLookCam.m_YAxisRecentering.m_enabled = false;
            freeLookCam.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
            _noFocusSettingsApplied = true;
        }
        else
        {
            // 恢复到项目默认（此处仅保持关闭，若需开启可在 Inspector 设置并在此读取）
            // 不主动开启，避免在有焦点时自动回中影响玩家操控
            _noFocusSettingsApplied = false;
        }
    }

    private void TryInitializeGlobalView()
    {
        if (freeLookCam == null || _pivot == null) return;
        // 若已经有 focusTarget 则不覆盖
        if (_focusTarget != null) return;
        Bounds b;
        if (!ComputeSceneBounds(out b)) return;
        Vector3 center = b.center;
        // 提升高度以得到俯视
        center.y = Mathf.Max(center.y, globalViewHeight);
        _pivot.position = center;
        // 设置初始轴角度
        freeLookCam.m_YAxis.Value = Mathf.Clamp01(globalViewYAxisValue);
        freeLookCam.m_XAxis.Value = globalViewYaw % 360f;
        _cachedXAxis = freeLookCam.m_XAxis.Value;
        _cachedYAxis = freeLookCam.m_YAxis.Value;
        // 根据场景尺寸决定缩放（越大越远）
        float sceneRadius = b.extents.magnitude + globalViewPadding;
        if (_baseOrbits != null && _baseOrbits.Length == 3)
        {
            // 估算需要的缩放比例：半径 / 基础平均半径
            float baseAvg = (_baseOrbits[0].m_Radius + _baseOrbits[1].m_Radius + _baseOrbits[2].m_Radius) / 3f;
            float needScale = baseAvg > 0.001f ? sceneRadius / baseAvg : 1f;
            _zoomTarget = _zoomCurrent = Mathf.Clamp(needScale, minZoomScale, maxZoomScale);
            ApplyZoomToOrbits(_zoomCurrent);
        }
        ApplyAxisInversion();
    }

    private bool ComputeSceneBounds(out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        var units = FindObjectsOfType<BattleUnit>();
        if (units != null && units.Length > 0)
        {
            bool init = false;
            foreach (var u in units)
            {
                if (u == null) continue;
                var p = u.transform.position;
                if (!init)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    init = true;
                }
                else
                {
                    bounds.Encapsulate(p);
                }
            }
            if (init)
            {
                // 扩展 padding
                bounds.Expand(new Vector3(globalViewPadding, 0f, globalViewPadding));
                return true;
            }
        }
        // 回退：使用场景中所有渲染器
        var rends = FindObjectsOfType<Renderer>();
        bool initR = false;
        foreach (var r in rends)
        {
            if (r == null) continue;
            if (!initR)
            {
                bounds = r.bounds;
                initR = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        if (initR)
        {
            bounds.Expand(globalViewPadding);
            return true;
        }
        return false;
    }

#if UNITY_EDITOR
    // Editor 下参数变动时立即生效
    private void OnValidate()
    {
        ApplyAxisSensitivity();
        ApplyAxisInversion();
        ApplyNoFocusSettings(_focusTarget == null);
    }
#endif
}
