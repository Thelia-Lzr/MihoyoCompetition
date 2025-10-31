using System.Collections;
using UnityEngine;
using Cinemachine;

/// <summary>
/// 简单的战斗镜头控制器，利用 Cinemachine 切换虚拟相机并提供聚焦/动作镜头协程
/// 接线步骤见注释：将不同的 VirtualCamera 指向对应的 public 字段，并在场景中挂入此脚本。
/// </summary>
public class BattleCameraController : MonoBehaviour
{
    public CinemachineVirtualCamera overviewCam;
    public CinemachineVirtualCamera focusCam;
    public CinemachineVirtualCamera actionCam;
    public CinemachineTargetGroup targetGroup;

    // 更快的过渡默认值
    public float blendTime =0.15f;

    // 鼠标控制参数（提高灵敏度）
    public float mouseSensitivity =0.12f;
    public float minPitch = -80f;
    public float maxPitch =80f;

    // 内部状态用于鼠标轨道控制
    private bool mouseControlEnabled = false;
    private float orbitYaw =0f; // radians
    private float orbitPitch =0f; // radians
    public float orbitRadius =3f; // exposed to inspector
    private Vector3 currentOffset;

    void Start()
    {
        // 可根据需要在 Start 中做默认优先级设置
        ShowOverview();
    }

    void Update()
    {
        if (!mouseControlEnabled) return;

        if (focusCam == null || focusCam.Follow == null) return;

        // 不再需要按下右键，直接使用鼠标移动来旋转（当启用鼠标控制时）
        float dx = Input.GetAxis("Mouse X");
        float dy = Input.GetAxis("Mouse Y");

        // 当鼠标没有移动时不更新
        if (Mathf.Approximately(dx,0f) && Mathf.Approximately(dy,0f)) return;

        orbitYaw += dx * mouseSensitivity;
        orbitPitch -= dy * mouseSensitivity;

        orbitPitch = Mathf.Clamp(orbitPitch, minPitch * Mathf.Deg2Rad, maxPitch * Mathf.Deg2Rad);

        //由球面坐标计算偏移
        float y = orbitRadius * Mathf.Sin(orbitPitch);
        float horiz = orbitRadius * Mathf.Cos(orbitPitch);
        float x = horiz * Mathf.Sin(orbitYaw);
        float z = horiz * Mathf.Cos(orbitYaw);

        currentOffset = new Vector3(x, y, z);

        var transposer = focusCam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
        {
            transposer.m_FollowOffset = currentOffset;
        }
    }

    public void ShowOverview()
    {
        if (overviewCam != null) overviewCam.Priority =30;
        if (focusCam != null) focusCam.Priority =10;
        if (actionCam != null) actionCam.Priority =5;
    }

    public IEnumerator FocusOn(Transform unit, Vector3 followOffset, float holdSeconds)
    {
        if (focusCam == null || unit == null)
        {
            yield return new WaitForSeconds(holdSeconds + blendTime);
            yield break;
        }

        focusCam.Follow = unit;
        focusCam.LookAt = unit;
        var transposer = focusCam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
        {
            transposer.m_FollowOffset = followOffset;
        }

        // 提高优先级以激活
        focusCam.Priority =50;
        if (overviewCam != null) overviewCam.Priority =10;
        if (actionCam != null) actionCam.Priority =5;

        // 等待 blend 到位并保持一定时间
        yield return new WaitForSeconds(blendTime + holdSeconds);

        // 恢复为 overview（该方法用于短暂切换场景时恢复）
        ShowOverview();
    }

    /// <summary>
    ///立即将焦点设置到指定单位（不做等待），用于持续追踪当前行动单位
    /// </summary>
    public void FocusImmediate(Transform unit, Vector3 followOffset)
    {
        if (focusCam == null || unit == null) return;

        focusCam.Follow = unit;
        focusCam.LookAt = unit;
        var transposer = focusCam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
        {
            transposer.m_FollowOffset = followOffset;
        }

        // 更新内部轨道状态
        currentOffset = followOffset;
        orbitRadius = currentOffset.magnitude;
        // initial yaw/pitch from offset
        orbitYaw = Mathf.Atan2(currentOffset.x, currentOffset.z);
        orbitPitch = Mathf.Asin(Mathf.Clamp(currentOffset.y / orbitRadius, -1f,1f));

        // 提高优先级以激活
        focusCam.Priority =50;
        if (overviewCam != null) overviewCam.Priority =10;
        if (actionCam != null) actionCam.Priority =5;
    }

    public IEnumerator PlayActionCam(Transform actor, Transform target, Vector3 offset, float actionDuration)
    {
        if (actionCam == null || actor == null)
        {
            yield return new WaitForSeconds(actionDuration);
            yield break;
        }

        actionCam.Follow = actor;
        actionCam.LookAt = target ?? actor;
        var t = actionCam.GetCinemachineComponent<CinemachineTransposer>();
        if (t != null)
        {
            t.m_FollowOffset = offset;
        }

        actionCam.Priority =60;
        if (focusCam != null) focusCam.Priority =10;
        if (overviewCam != null) overviewCam.Priority =5;

        yield return new WaitForSeconds(actionDuration);

        // 恢复为聚焦镜头而不是 overview，避免每次都经过 overview
        if (focusCam != null) focusCam.Priority =50;
        if (overviewCam != null) overviewCam.Priority =10;
        if (actionCam != null) actionCam.Priority =5;
    }

    // 将单位加入或从 targetGroup 中移除，便于全景展示
    public void AddToTargetGroup(Transform unit, float weight =1f, float radius =1f)
    {
        if (targetGroup == null || unit == null) return;
        targetGroup.AddMember(unit, weight, radius);
    }

    public void RemoveFromTargetGroup(Transform unit)
    {
        if (targetGroup == null || unit == null) return;
        // CinemachineTargetGroup 没有直接的 RemoveMember(Transform) API，构建列表并移除匹配项
        var list = new System.Collections.Generic.List<Cinemachine.CinemachineTargetGroup.Target>(targetGroup.m_Targets);
        for (int i =0; i < list.Count; i++)
        {
            if (list[i].target == unit)
            {
                list.RemoveAt(i);
                targetGroup.m_Targets = list.ToArray();
                break;
            }
        }
    }

    /// <summary>
    /// 平滑从一个单位过渡到另一个单位的焦点，避免画面瞬切。
    /// 使用二次贝塞尔曲线来使过渡更有电影感。
    /// 在转场期间会自动禁用鼠标输入，转场完成后会恢复之前的鼠标使能状态。
    /// </summary>
    public IEnumerator TransitionToTarget(Transform from, Transform to, Vector3 targetOffset, float duration)
    {
        if (focusCam == null || from == null || to == null)
        {
            FocusImmediate(to, targetOffset);
            yield break;
        }

        // remember previous mouse state and disable mouse control during transition
        bool prevMouse = mouseControlEnabled;
        EnableMouseControl(false);

        // Create a temporary proxy that will move along a bezier path from "from" to "to"
        GameObject proxy = new GameObject("CameraTransitionProxy");
        proxy.transform.position = from.position;

        // Set focusCam to follow proxy so camera moves smoothly
        focusCam.Follow = proxy.transform;
        focusCam.LookAt = proxy.transform;

        var transposer = focusCam.GetCinemachineComponent<CinemachineTransposer>();
        Vector3 startOffset = transposer != null ? transposer.m_FollowOffset : Vector3.zero;
        float elapsed =0f;

        // Build a control point above the midpoint for an arc
        Vector3 p0 = from.position;
        Vector3 p2 = to.position;
        Vector3 midpoint = (p0 + p2) *0.5f;
        float distance = Vector3.Distance(p0, p2);
        float height = Mathf.Max(0.5f, distance *0.3f);
        Vector3 control = midpoint + Vector3.up * height;

        while (elapsed < duration)
        {
            if (proxy == null) break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Quadratic Bezier interpolation: B(t) = (1-t)^2*p0 +2(1-t)t*p1 + t^2*p2
            float u =1f - t;
            Vector3 pos = u * u * p0 +2f * u * t * control + t * t * p2;
            proxy.transform.position = pos;

            // lerp offset if transposer exists
            if (transposer != null)
            {
                transposer.m_FollowOffset = Vector3.Lerp(startOffset, targetOffset, t);
                currentOffset = transposer.m_FollowOffset;
            }

            yield return null;
        }

        // Ensure final state
        if (transposer != null)
        {
            transposer.m_FollowOffset = targetOffset;
            currentOffset = targetOffset;
        }

        focusCam.Follow = to;
        focusCam.LookAt = to;

        // destroy proxy
        Destroy(proxy);

        // restore previous mouse state
        EnableMouseControl(prevMouse);
    }

    /// <summary>
    /// 启用或禁用鼠标轨道控制
    /// 当启用时，用户移动鼠标会旋转镜头
    /// </summary>
    public void EnableMouseControl(bool enable)
    {
        mouseControlEnabled = enable;
        if (enable)
        {
            // 初始化轨道参数
            var transposer = focusCam != null ? focusCam.GetCinemachineComponent<CinemachineTransposer>() : null;
            if (transposer != null)
            {
                currentOffset = transposer.m_FollowOffset;
                orbitRadius = currentOffset.magnitude;
                orbitYaw = Mathf.Atan2(currentOffset.x, currentOffset.z);
                orbitPitch = Mathf.Asin(Mathf.Clamp(currentOffset.y / orbitRadius, -1f,1f));
            }
        }
    }
}
