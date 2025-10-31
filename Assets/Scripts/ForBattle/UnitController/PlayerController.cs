using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.ForBattle.UI;
using Assets.Scripts.ForBattle.Indicators;
using Assets.Scripts.ForBattle;
using Assets.Scripts.ForBattle.Audio;

/// <summary>
/// 玩家控制器基类：提供战斗UI、移动、目标选择与范围指示器功能。
/// 子类可继承此类实现特定角色的技能。
/// </summary>
public class PlayerController : BattleUnitController
{
    [Header("References")]
    public BattleCanvasController battleUI;
    public BattleIndicatorManager indicatorManager;
    public SkillSystem skillSystem;
    public BattleCameraController cameraController;
    public SfxPlayer sfxPlayer;

    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    public float moveRange = 5f; // 移动限制范围
    private Vector3 originalPosition;
    private bool isMoving = false;
    [Tooltip("Assign a child transform that contains only the visible model. If null, the controller will not rotate the root transform to avoid rotating attached cameras.")]
    public Transform visualRoot;

    [Header("Target Selection Settings")]
    public float targetSelectionRange = 10f;
    public float sectorAngle = 60f;
    public float circleRadius = 3f;
    public LayerMask unitLayerMask;

    // 当前选择模式
    protected enum SelectionMode
    {
        None,
        TargetSelection,  // 目标选择
        AreaSelection,    // 区域选择
        DirectionSelection // 方向选择
    }

    protected SelectionMode currentSelectionMode = SelectionMode.None;
    protected BattleUnit selectedTarget = null;
    protected Vector3 selectedArea = Vector3.zero;
    protected Vector3 selectedDirection = Vector3.forward;

    public override void OnBattleStart()
    {
        // 查找引用（如果未在 Inspector 绑定）
        if (battleUI == null)
            battleUI = FindObjectOfType<BattleCanvasController>();
        if (indicatorManager == null)
            indicatorManager = FindObjectOfType<BattleIndicatorManager>();
        if (skillSystem == null)
            skillSystem = FindObjectOfType<SkillSystem>();
    }

    public override void OnBattleEnd()
    {
        if (battleUI != null)
            battleUI.HideUI();
        if (indicatorManager != null)
            indicatorManager.ClearAll();
    }

    

    public override IEnumerator ExecuteTurn(BattleTurnManager turnManager)
    {
        if (unit == null) yield break;

        originalPosition = transform.position;
        isMoving = false;

        Debug.Log($"[PlayerController] {unit.unitName} 的回合开始");

        // 显示战斗UI并等待玩家选择行动类型
        BattleCanvasController.BattleActionType selectedAction = BattleCanvasController.BattleActionType.Attack;
        bool actionConfirmed = false;

        if (battleUI != null)
        {
            battleUI.ShowUI(unit, (action) =>
            {
                selectedAction = action;
                actionConfirmed = true;
            });
        }
        else
        {
            Debug.LogWarning("BattleCanvasController 未绑定，使用默认攻击");
            actionConfirmed = true;
        }

        // 等待玩家确认或允许移动
        while (!actionConfirmed)
        {
            // 允许 WASD 移动
            HandleMovement();
            // 允许QE切换选中
            HandleSelectionSwitch();

            yield return null;
        }

        // 隐藏UI
        if (battleUI != null)
            battleUI.HideUI();

        // 根据选择的行动类型执行
        switch (selectedAction)
        {
            case BattleCanvasController.BattleActionType.Attack:
                yield return ExecuteAttack();
                break;
            case BattleCanvasController.BattleActionType.Skill:
                yield return ExecuteSkill();
                break;
            case BattleCanvasController.BattleActionType.Item:
                yield return ExecuteItem();
                break;
            case BattleCanvasController.BattleActionType.Escape:
                yield return ExecuteEscape();
                break;
        }

        // 清理指示器
        if (indicatorManager != null)
            indicatorManager.ClearAll();

        Debug.Log($"[PlayerController] {unit.unitName} 回合结束");
    }

    /// <summary>
    /// QE选择处理
    /// <summary>
    protected virtual void HandleSelectionSwitch()
    {
        if (battleUI == null) return;
        // Q 切换到上一个选项
        if (Input.GetKeyDown(KeyCode.Q))
        {
            // cycle forward
            int next = ((int)battleUI.Choice +1) %4;
            battleUI.Choice = (Assets.Scripts.ForBattle.UI.BattleCanvasController.BattleActionType)next;
            sfxPlayer.Play("ChangeChoice");
        }
        // E 切换到下一个选项
        if (Input.GetKeyDown(KeyCode.E))
        {
            int prev = ((int)battleUI.Choice -1);
            if (prev <0) prev =3;
            battleUI.Choice = (Assets.Scripts.ForBattle.UI.BattleCanvasController.BattleActionType)prev;
            sfxPlayer.Play("ChangeChoice");
        }
        battleUI.Refresh();
    }
    /// <summary>
    /// WASD 移动处理（限制在一定范围内）
    /// </summary>
    protected virtual void HandleMovement()
    {
        if (unit == null) return;

        // Read raw WASD input as axes
        float h =0f;
        float v =0f;
        if (Input.GetKey(KeyCode.W)) v +=1f;
        if (Input.GetKey(KeyCode.S)) v -=1f;
        if (Input.GetKey(KeyCode.D)) h +=1f;
        if (Input.GetKey(KeyCode.A)) h -=1f;

        Vector3 moveInput = new Vector3(h,0f, v);
        if (moveInput.sqrMagnitude <=0.0001f) return;

        // Determine camera basis (prefer camera from cameraController if provided)
        Camera cam = null;
        if (cameraController != null && cameraController.gameObject != null)
        {
            cam = Camera.main; // cameraController may not expose camera directly; prefer main camera
        }
        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            // fallback to world-relative movement
            moveInput = moveInput.normalized;
        }
        else
        {
            // Convert input (h,v) into camera-relative movement on the XZ plane
            Vector3 camForward = cam.transform.forward;
            camForward.y =0f;
            camForward.Normalize();
            Vector3 camRight = cam.transform.right;
            camRight.y =0f;
            camRight.Normalize();

            // moveInput.z is forward/back, x is right/left
            Vector3 camMove = camRight * moveInput.x + camForward * moveInput.z;
            if (camMove.sqrMagnitude >0.0001f) moveInput = camMove.normalized;
            else return;
        }

        // Apply movement
        Vector3 newPos = transform.position + moveInput * moveSpeed * Time.deltaTime;

        // Clamp movement within allowed range from original position
        if (Vector3.Distance(originalPosition, newPos) <= moveRange)
        {
            transform.position = newPos;
            unit.battlePos = new Vector2(newPos.x, newPos.z);

            // Do not rotate the root or visual model here. WASD should only move the unit
            // relative to the camera forward/right directions and must not affect camera angle.
            // If you want the visual model to face movement direction without affecting the
            // camera, handle that in the character's animation controller or set `visualRoot`
            // rotation elsewhere. Leaving this blank ensures camera angle remains unchanged.
        }
    }

    // ===== 行动执行 =====

    protected virtual IEnumerator ExecuteAttack()
    {
        Debug.Log("[PlayerController] 执行普通攻击");

        // 使用目标选择模式
        yield return SelectTarget();

        if (selectedTarget != null && skillSystem != null)
        {
            Debug.Log($"攻击目标: {selectedTarget.unitName}");
            skillSystem.CauseDamage(selectedTarget, unit.battleAtk, DamageType.Physics);
            yield return new WaitForSeconds(0.5f);
        }
        else
        {
            Debug.Log("未选择有效目标，跳过攻击");
        }
    }

    protected virtual IEnumerator ExecuteSkill()
    {
        Debug.Log("[PlayerController] 执行技能（子类应重写此方法）");
        // 子类在此实现特定技能逻辑
        yield return new WaitForSeconds(0.3f);
    }

    protected virtual IEnumerator ExecuteItem()
    {
        Debug.Log("[PlayerController] 使用道具");
        yield return new WaitForSeconds(0.3f);
    }

    protected virtual IEnumerator ExecuteEscape()
    {
        Debug.Log("[PlayerController] 尝试逃跑");
        yield return new WaitForSeconds(0.3f);
    }

    // ===== 三种选择模式 =====

    /// <summary>
    /// 目标选择：在限定范围内选择一个敌方单位
    /// </summary>
    protected IEnumerator SelectTarget()
    {
        currentSelectionMode = SelectionMode.TargetSelection;
        selectedTarget = null;

        if (battleUI != null)
            battleUI.UpdatePrompt("选择目标 (Tab切换/空格确认/Esc取消)");

        List<BattleUnit> validTargets = GetValidTargets();
        int currentIndex = 0;

        while (currentSelectionMode == SelectionMode.TargetSelection)
        {
            // Tab 切换目标
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (validTargets.Count > 0)
                {
                    currentIndex = (currentIndex + 1) % validTargets.Count;
                }
            }

            // 显示当前目标标记
            if (validTargets.Count > 0 && indicatorManager != null)
            {
                selectedTarget = validTargets[currentIndex];
                indicatorManager.ShowTargetMarker(selectedTarget.transform);
            }

            // 空格确认
            if (Input.GetKeyDown(KeyCode.Space))
            {
                currentSelectionMode = SelectionMode.None;
                break;
            }

            // Esc 取消
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                selectedTarget = null;
                currentSelectionMode = SelectionMode.None;
                break;
            }

            yield return null;
        }

        if (indicatorManager != null)
            indicatorManager.ClearAll();
    }

    /// <summary>
    /// 区域选择：选择一个世界坐标作为技能释放中心
    /// </summary>
    protected IEnumerator SelectArea(float radius)
    {
        currentSelectionMode = SelectionMode.AreaSelection;
        selectedArea = transform.position;

        if (battleUI != null)
            battleUI.UpdatePrompt("选择区域 (鼠标移动/空格确认/Esc取消)");

        Camera cam = Camera.main ?? Camera.allCameras[0];
        if (cam == null)
        {
            Debug.LogWarning("未找到摄像机，无法选择区域");
            currentSelectionMode = SelectionMode.None;
            yield break;
        }

        while (currentSelectionMode == SelectionMode.AreaSelection)
        {
            // 鼠标射线检测地面
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                Vector3 hitPos = hit.point;

                // 限制在范围内
                if (Vector3.Distance(transform.position, hitPos) <= targetSelectionRange)
                {
                    selectedArea = hitPos;
                    if (indicatorManager != null)
                        indicatorManager.ShowCircleIndicator(hitPos, radius, true);
                }
                else
                {
                    if (indicatorManager != null)
                        indicatorManager.ShowCircleIndicator(hitPos, radius, false);
                }
            }

            // 空格确认
            if (Input.GetKeyDown(KeyCode.Space))
            {
                currentSelectionMode = SelectionMode.None;
                break;
            }

            // Esc 取消
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                currentSelectionMode = SelectionMode.None;
                break;
            }

            yield return null;
        }

        if (indicatorManager != null)
            indicatorManager.ClearAll();
    }

    /// <summary>
    /// 方向选择：选择镜头朝向作为技能方向
    /// </summary>
    protected IEnumerator SelectDirection()
    {
        currentSelectionMode = SelectionMode.DirectionSelection;

        if (battleUI != null)
            battleUI.UpdatePrompt("调整方向 (鼠标/空格确认/Esc取消)");

        Camera cam = Camera.main ?? Camera.allCameras[0];
        if (cam == null)
        {
            selectedDirection = transform.forward;
            currentSelectionMode = SelectionMode.None;
            yield break;
        }

        // 显示扇形指示器
        if (indicatorManager != null)
            indicatorManager.ShowSectorIndicator(transform, targetSelectionRange, sectorAngle);

        while (currentSelectionMode == SelectionMode.DirectionSelection)
        {
            // 获取镜头前向（投影到水平面）
            Vector3 camForward = cam.transform.forward;
            camForward.y = 0f;
            if (camForward.sqrMagnitude > 0.01f)
            {
                selectedDirection = camForward.normalized;

                // 更新扇形指示器朝向
                if (indicatorManager != null)
                    indicatorManager.UpdateSectorRotation(transform, selectedDirection);
            }

            // 空格确认
            if (Input.GetKeyDown(KeyCode.Space))
            {
                currentSelectionMode = SelectionMode.None;
                break;
            }

            // Esc 取消
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                currentSelectionMode = SelectionMode.None;
                break;
            }

            yield return null;
        }

        if (indicatorManager != null)
            indicatorManager.ClearAll();
    }

    // ===== 辅助方法 =====

    /// <summary>
    /// 获取有效目标列表（敌方单位，在范围内）
    /// </summary>
    protected List<BattleUnit> GetValidTargets()
    {
        List<BattleUnit> targets = new List<BattleUnit>();
        BattleUnit[] allUnits = FindObjectsOfType<BattleUnit>();

        foreach (var u in allUnits)
        {
            if (u == null || u == unit) continue;

            // 只选择敌方单位
            if (unit.unitType == BattleUnitType.Player && u.unitType == BattleUnitType.Enemy)
            {
                // 检查距离
                if (Vector3.Distance(transform.position, u.transform.position) <= targetSelectionRange)
                {
                    targets.Add(u);
                }
            }
            else if (unit.unitType == BattleUnitType.Enemy && u.unitType == BattleUnitType.Player)
            {
                if (Vector3.Distance(transform.position, u.transform.position) <= targetSelectionRange)
                {
                    targets.Add(u);
                }
            }
        }

        return targets;
    }

    /// <summary>
    /// 供子类调用：使用目标选择
    /// </summary>
    protected IEnumerator UseTargetSelection(System.Action<BattleUnit> onTargetSelected)
    {
        yield return SelectTarget();
        onTargetSelected?.Invoke(selectedTarget);
    }

    /// <summary>
    /// 供子类调用：使用区域选择
    /// </summary>
    protected IEnumerator UseAreaSelection(float radius, System.Action<Vector3> onAreaSelected)
    {
        yield return SelectArea(radius);
        onAreaSelected?.Invoke(selectedArea);
    }

    /// <summary>
    /// 供子类调用：使用方向选择
    /// </summary>
    protected IEnumerator UseDirectionSelection(System.Action<Vector3> onDirectionSelected)
    {
        yield return SelectDirection();
        onDirectionSelected?.Invoke(selectedDirection);
    }
}
