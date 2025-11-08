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
    //public SfxPlayer sfxPlayer;
    [Header("Audio")]
    public string stepLoopKey = "Steps"; // key in SfxPlayer sounds table for footsteps loop

    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    public float moveRange = 5f; // 移动限制范围
    private Vector3 originalPosition;
    private bool isMoving = false;
    [Tooltip("Assign a child transform that contains only the visible model. If null, the controller will not rotate the root transform to avoid rotating attached cameras.")]
    public Transform visualRoot;

    [Header("Animation Settings")]
    [Tooltip("Rotate the visual model to face movement direction (recommended for VRM). If visualRoot is set, only that transform rotates.")]
    public bool faceMoveDirection = true;
    [Tooltip("Slerp speed for facing rotation.")]
    public float turnSpeed = 10f;
    [Tooltip("Animator float parameter used by locomotion blend tree (e.g., 'Speed').")]
    public string animatorSpeedParam = "Speed";
    [Tooltip("Normalize speed sent to Animator (0..1). If false, sends units/sec.")]
    public bool normalizeAnimatorSpeed = true;
    [Tooltip("Multiplier applied to the value sent to Animator.")]
    public float animatorSpeedScale = 1f;
    private Animator cachedAnimator;
    [Tooltip("Optional Animator float parameter for turning (Unity-Chan uses 'Direction').")]
    public string animatorDirectionParam = "Direction";
    [Tooltip("Optional Animator bool parameter for grounded state.")]
    public string animatorGroundedParam = "Grounded";
    [Tooltip("Optional Animator float parameter for forward speed (Standard Assets uses 'Forward').")]
    public string animatorForwardParam = "Forward";
    [Tooltip("Optional Animator float parameter for turn amount (Standard Assets uses 'Turn').")]
    public string animatorTurnParam = "Turn";
    private bool animHasSpeedParam;
    private bool animHasDirectionParam;
    private bool animHasGroundedParam;
    private bool animHasForwardParam;
    private bool animHasTurnParam;

    [Header("Target Selection Settings")]
    public float targetSelectionRange = 3f;
    public float sectorAngle = 60f;
    public float circleRadius = 3f;
    public LayerMask unitLayerMask;
    [Header("Combat Ranges")]
    [Tooltip("近战范围（用于普攻和近战选择性技能） ")]
    public float meleeRange = 2.5f;

    [Header("Chain Settings")]
    [Tooltip("连携范围半径")] public float chainRange = 1f;
    private BattleUnit currentChainPartner = null;
    private GameObject chainCircleSelf = null;
    private GameObject chainCircleAlly = null;

    protected bool end;

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
    // preselection fields used when confirming via mouse click
    protected int preselectedSkillIndex = -1;
    protected BattleUnit preselectedTarget = null;
    // cached skill list signature to avoid resetting SkillListController every frame
    private string skillListSignature = null;
    private int prevShownSkillIndex = -1;
    // track last menu choice to detect transitions (so entering Skill forces preview update)
    private int lastChoice = -1;
    // keep reference to created movement range indicator
    protected GameObject movementRangeIndicator = null;
    // indicator that follows the unit to show current skill cast range
    protected GameObject skillRangeIndicator = null;
    protected GameObject attackRangeIndicator = null;
    // track if a skill has been executed directly (quick cast)
    private bool skillQuickCasted = false;
    // request to reopen selection if a skill couldn't execute due to invalid target/resources
    protected bool skillReselectRequested = false;
    // track previous frame position to compute actual velocity for animator
    private Vector3 _prevFramePos;

    private List<BattleUnit> skillCandidateTargets = new List<BattleUnit>();
    private int skillTargetCycleIndex = 0;

    private List<BattleUnit> attackCandidateTargets = new List<BattleUnit>();
    private int attackTargetCycleIndex = 0;
    private BattleUnit currentAttackTarget = null; // 当前普攻预览目标

    public override void OnBattleStart()
    {
        // 查找引用（如果未在 Inspector 绑定）
        if (battleUI == null)
            battleUI = FindObjectOfType<BattleCanvasController>();
        if (indicatorManager == null)
            indicatorManager = FindObjectOfType<BattleIndicatorManager>();
        if (skillSystem == null)
            skillSystem = FindObjectOfType<SkillSystem>();
        // Cache Animator from visual root (VRM Animator usually lives on a child)
        if (cachedAnimator == null)
        {
            var root = visualRoot != null ? visualRoot : transform;
            cachedAnimator = root.GetComponentInChildren<Animator>();
        }
        MapAnimatorParameters();
    }

    public override void OnBattleEnd()
    {
        if (battleUI != null)
            battleUI.HideUI();
        if (indicatorManager != null)
            indicatorManager.ClearAll();
        // stop footstep loop when battle ends
        if (sfxPlayer != null) sfxPlayer.StopLoop(stepLoopKey);
    }

    public override IEnumerator ExecuteTurn(BattleTurnManager turnManager)
    {
        if (unit == null) yield break;

        originalPosition = transform.position;
        isMoving = false;
        _prevFramePos = transform.position;

        Debug.Log($"[PlayerController] {unit.unitName} 的回合开始");
        skillQuickCasted = false;
        // Reset skill list cache on new turn to ensure correct skills after unit switches
        skillListSignature = null;
        prevShownSkillIndex = -1;

        // 单次选择与执行流程
        BattleCanvasController.BattleActionType selectedAction = BattleCanvasController.BattleActionType.Attack;
        bool actionConfirmed = false;

        // 创建移动范围指示器（使用MovementRange标签，自动清理同标签旧指示器）
        if (indicatorManager != null)
        {
            // Create and keep reference to movement range indicator (hollow, tagged, single-instance)
            movementRangeIndicator = indicatorManager.CreateCircleIndicator(
                        GetGroundPosition(originalPosition),
              moveRange,
                  true,  // 有效
                true,  // 空心圈
              BattleIndicatorManager.Tags.MovementRange,  // 使用标签
                            true   // 清理同标签旧指示器
                     );
        }

        //选择与执行循环：当技能执行失败并请求重选时，回到选择界面
        while (true)
        {
            // 每轮选择开始时重置重选标志
            skillReselectRequested = false;

            // 显示战斗UI并等待玩家选择行动类型
            actionConfirmed = false;
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
            SkillListController slc = (battleUI != null) ? battleUI.skillListController : null;
            BattleCanvasController.BattleActionType prevChoice;
            prevChoice = BattleCanvasController.BattleActionType.Attack;
            // 等待玩家确认或允许移动
            while (!actionConfirmed)
            {

                if (prevChoice != battleUI.Choice)
                {
                    indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.TargetMarker);
                    indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.AttackRange);
                    indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.SkillPreview);
                    //进入普攻模式时重置循环索引，默认从最近目标开始
                    if (battleUI.Choice == BattleCanvasController.BattleActionType.Attack)
                    {
                        attackTargetCycleIndex = 0;
                    }
                }
                // allow movement and QE selection
                HandleMovement();
                HandleSelectionSwitch(ref actionConfirmed);

                // Preview logic for Attack: target cycling like single-target skill
                if (battleUI != null && battleUI.Choice == BattleCanvasController.BattleActionType.Attack)
                {
                    // 构建候选（每帧刷新以适应移动），按距离排序
                    attackCandidateTargets.Clear();
                    var allUnits = FindObjectsOfType<BattleUnit>();
                    for (int i = 0; i < allUnits.Length; i++)
                    {
                        var u = allUnits[i];
                        if (!IsValidTarget(u)) continue;
                        if (Vector3.Distance(transform.position, u.transform.position) <= meleeRange)
                        {
                            attackCandidateTargets.Add(u);
                        }
                    }
                    // 按与自身距离升序排序（最近优先）
                    attackCandidateTargets.Sort((a, b) =>
                    {
                        float da = Vector3.SqrMagnitude(a.transform.position - transform.position);
                        float db = Vector3.SqrMagnitude(b.transform.position - transform.position);
                        return da.CompareTo(db);
                    });
                    // 鼠标指向优先
                    BattleUnit hovered = null;
                    Camera cam = Camera.main;
                    if (cam != null)
                    {
                        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                        {
                            var hu = hit.collider.GetComponentInParent<BattleUnit>();
                            if (hu != null && IsValidTarget(hu) && Vector3.Distance(transform.position, hu.transform.position) <= meleeRange)
                            {
                                hovered = hu;
                            }
                        }
                    }

                    // Tab 切换候选（仅当有候选时）
                    if (Input.GetKeyDown(KeyCode.Tab) && attackCandidateTargets.Count > 0)
                    {
                        attackTargetCycleIndex = (attackTargetCycleIndex + 1) % attackCandidateTargets.Count;
                    }
                    if (attackTargetCycleIndex >= attackCandidateTargets.Count) attackTargetCycleIndex = 0;

                    BattleUnit displayTarget = hovered;
                    if (displayTarget == null)
                    {
                        // 若没有鼠标指向，则使用当前循环索引（默认0即最近）
                        if (attackCandidateTargets.Count > 0)
                        {
                            displayTarget = attackCandidateTargets[attackTargetCycleIndex];
                        }
                    }
                    else
                    {
                        // 若有鼠标指向，将循环索引同步到该目标，便于继续按Tab从此处切换
                        int idx = attackCandidateTargets.IndexOf(displayTarget);
                        if (idx >= 0) attackTargetCycleIndex = idx;
                    }
                    currentAttackTarget = displayTarget;
                    if (displayTarget != null)
                    {
                        preselectedTarget = displayTarget;
                        if (indicatorManager != null)
                        {
                            indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.TargetMarker);
                            indicatorManager.CreateTargetMarker(displayTarget.transform, true);
                        }
                    }
                    else
                    {
                        preselectedTarget = null;
                        if (indicatorManager != null)
                            indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.TargetMarker);
                    }

                    // 显示普攻范围圈（跟随角色，空心）
                    if (indicatorManager != null)
                    {
                        Vector3 center = GetGroundPosition(transform.position);
                        if (attackRangeIndicator == null)
                        {
                            attackRangeIndicator = indicatorManager.CreateCircleIndicator(
                            center,
                            meleeRange,
                            true,
                            true,
                            BattleIndicatorManager.Tags.AttackRange,
                            true
                            );
                        }
                        else
                        {
                            indicatorManager.UpdateCircleIndicator(attackRangeIndicator, center, meleeRange, true);
                        }
                    }

                    // 连携：寻找最近的友方并显示连携圈，并播放进入/退出音效
                    var prevPartner = currentChainPartner;
                    BattleUnit bestPartner = FindBestChainPartner();
                    if (bestPartner != prevPartner)
                    {
                        if (prevPartner != null && sfxPlayer != null) sfxPlayer.Play("Out");
                        if (bestPartner != null && sfxPlayer != null) sfxPlayer.Play("In");
                        currentChainPartner = bestPartner;
                    }
                    UpdateChainVisuals(currentChainPartner);
                    // ===== end chain visuals =====
                }
                else
                {
                    if (indicatorManager != null)
                    {
                        indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.AttackRange);
                        indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.TargetMarker);
                        indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.ChainCircle);
                    }
                    if (currentChainPartner != null && sfxPlayer != null) sfxPlayer.Play("Out");
                    currentAttackTarget = null;
                    currentChainPartner = null;
                    chainCircleSelf = null;
                    chainCircleAlly = null;
                    preselectedTarget = null;
                }


                // If hovering Skill and we have a skill list, populate and show skills
                if (slc != null && battleUI != null && battleUI.Choice == BattleCanvasController.BattleActionType.Skill)
                {
                    var display = GetSkillDisplayStrings();
                    string sig = null;
                    if (display != null) sig = string.Join("|", display);
                    if (sig != skillListSignature && display != null)
                    {
                        slc.SetSkills(display);
                        skillListSignature = sig;
                        prevShownSkillIndex = -1;
                        // 切换技能列表时重置目标循环
                        skillTargetCycleIndex = 0;
                    }
                    slc.Show();

                    int currIdx = slc.GetSelectedIndex();
                    if (currIdx != prevShownSkillIndex)
                    {
                        indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.SkillPreview);
                        indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.SkillRange);
                        // 技能改变时重置目标循环
                        skillTargetCycleIndex = 0;
                    }
                    ShowSkillPreview(currIdx);

                    // 单体技能目标循环逻辑
                    if (IsSkillSingleTarget(currIdx))
                    {
                        // 构建候选列表：在施法范围内且有效
                        skillCandidateTargets.Clear();
                        float range = GetSkillCastRange(currIdx);
                        foreach (var u in FindObjectsOfType<BattleUnit>())
                        {
                            if (!IsValidTarget(u)) continue;
                            if (Vector3.Distance(transform.position, u.transform.position) <= range)
                            {
                                skillCandidateTargets.Add(u);
                            }
                        }
                        if (skillCandidateTargets.Count > 0)
                        {
                            // Tab 切换目标
                            if (Input.GetKeyDown(KeyCode.Tab))
                            {
                                skillTargetCycleIndex = (skillTargetCycleIndex + 1) % skillCandidateTargets.Count;
                            }
                            // 安全索引
                            if (skillTargetCycleIndex >= skillCandidateTargets.Count) skillTargetCycleIndex = 0;
                            var currentTarget = skillCandidateTargets[skillTargetCycleIndex];
                            preselectedTarget = currentTarget; //赋值用于点击确认
                            if (indicatorManager != null)
                            {
                                indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.TargetMarker);
                                indicatorManager.CreateTargetMarker(currentTarget.transform, true);
                            }
                        }
                        else
                        {
                            // 没有候选则清理标记
                            preselectedTarget = null;
                            indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.TargetMarker);
                        }
                    }
                    else
                    {
                        // 非单体技能不显示目标标记（清除）
                        indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.TargetMarker);
                        preselectedTarget = null;
                    }

                    prevShownSkillIndex = currIdx;
                }
                else if (slc != null)
                {
                    slc.Hide();
                    skillListSignature = null;
                    prevShownSkillIndex = -1;
                    //退出技能选择时清理所有技能相关指示器（保留普攻的目标标记）
                    if (indicatorManager != null)
                    {
                        indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.SkillPreview);
                        indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.SkillRange);
                        // 不要在这里清理 TargetMarker，普攻模式需要它
                        skillRangeIndicator = null;
                    }
                    preselectedTarget = null;
                }

                // 当不在攻击模式也不在单体技能模式时始终清理目标标记
                bool inAttack = battleUI != null && battleUI.Choice == BattleCanvasController.BattleActionType.Attack;
                bool inSingleTargetSkill = battleUI != null && battleUI.Choice == BattleCanvasController.BattleActionType.Skill && IsSkillSingleTarget((slc != null) ? slc.GetSelectedIndex() : -1);
                if (!inAttack && !inSingleTargetSkill)
                {
                    if (indicatorManager != null)
                    {
                        indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.TargetMarker);
                    }
                    preselectedTarget = null;
                }

                // Confirm with left mouse button
                if (Input.GetMouseButtonDown(0))
                {
                    if (battleUI != null && battleUI.Choice == BattleCanvasController.BattleActionType.Attack)
                    {
                        BattleUnit toAttack = preselectedTarget != null ? preselectedTarget : currentAttackTarget;
                        if (toAttack != null)
                        {
                            var partnerSnapshot = currentChainPartner; // 捕获当下连携伙伴
                            preselectedTarget = toAttack; // ensure ExecuteAttack uses same
                            selectedAction = BattleCanvasController.BattleActionType.Attack;
                            yield return ExecuteAttack();
                            // 连携伙伴额外普攻（不影响其行动点）
                            if (partnerSnapshot != null && partnerSnapshot.battleHp > 0)
                            {
                                yield return StartCoroutine(PerformChainAttack(partnerSnapshot, toAttack));
                            }
                            actionConfirmed = true;
                        }
                        else
                        {
                            if (sfxPlayer != null) sfxPlayer.Play("Error");
                        }
                    }
                    else if (battleUI != null && battleUI.Choice == BattleCanvasController.BattleActionType.Skill)
                    {
                        // 尝试快速释放：仅在成功时才确认并关闭UI；失败则停留在选择界面以便重选
                        int idx = (slc != null) ? slc.GetSelectedIndex() : -1;
                        // 在尝试前重置重选标志，由技能在失败时设置
                        skillReselectRequested = false;
                        yield return TryQuickCastSkill(idx);
                        if (skillReselectRequested == false)
                        {
                            actionConfirmed = true;
                        }
                    }
                }
                prevChoice = battleUI.Choice;
                yield return null;
            }

            // hide skill list and any preview markers when leaving selection phase
            if (slc != null) slc.Hide();
            if (indicatorManager != null)
            {
                indicatorManager.ClearTargetMarkers();
                // 改为仅清理临时指示器，保留结界 (Barrier) 指示器
                indicatorManager.ClearEphemeralIndicators();
                indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.ChainCircle);
                skillRangeIndicator = null;
                attackRangeIndicator = null;
            }

            if (battleUI != null) battleUI.HideUI();
            // 否则退出循环，结束回合
            break;
        }

        // 清理指示但保留结界范围圈
        if (indicatorManager != null)
        {
            indicatorManager.ClearEphemeralIndicators();
            movementRangeIndicator = null;
            skillRangeIndicator = null;
        }

        // Ensure footstep loop is stopped at the end of the turn
        if (sfxPlayer != null && !string.IsNullOrEmpty(stepLoopKey))
        {
            sfxPlayer.StopLoop(stepLoopKey);
            isMoving = false;
        }

        ResetMovementState();

        Debug.Log($"[PlayerController] {unit.unitName} 回合结束");
    }

    /// <summary>
    /// QE选择处理
    /// <summary>
    protected virtual void HandleSelectionSwitch(ref bool actionConfirmed)
    {
        if (battleUI == null) return;
        // Q 切换到上一个选项
        if (Input.GetKeyDown(KeyCode.Q))
        {
            // cycle forward
            int next = ((int)battleUI.Choice + 1) % 4;
            battleUI.Choice = (Assets.Scripts.ForBattle.UI.BattleCanvasController.BattleActionType)next;
            if (sfxPlayer != null) sfxPlayer.Play("ChangeChoice");
        }
        // E 切换到下一个选项
        if (Input.GetKeyDown(KeyCode.E))
        {
            int prev = ((int)battleUI.Choice - 1);
            if (prev < 0) prev = 3;
            battleUI.Choice = (Assets.Scripts.ForBattle.UI.BattleCanvasController.BattleActionType)prev;
            if (sfxPlayer != null) sfxPlayer.Play("ChangeChoice");
        }
        battleUI.Refresh();
    }
    /// <summary>
    /// WASD 移动处理（限制在一定范围内）
    /// </summary>
    protected virtual void HandleMovement()
    {
        if (unit == null) return;
        if (cachedAnimator == null)
        {
            // Try to re-cache animator if model swapped at runtime
            var root = visualRoot != null ? visualRoot : transform;
            cachedAnimator = root.GetComponentInChildren<Animator>();
            if (cachedAnimator != null) MapAnimatorParameters();
        }

        // Read raw WASD input as axes
        float h = 0f;
        float v = 0f;
        if (Input.GetKey(KeyCode.W)) v += 1f;
        if (Input.GetKey(KeyCode.S)) v -= 1f;
        if (Input.GetKey(KeyCode.D)) h += 1f;
        if (Input.GetKey(KeyCode.A)) h -= 1f;

        Vector3 moveInput = new Vector3(h, 0f, v);
        bool inputMoving = Mathf.Abs(h) > 0.001f || Mathf.Abs(v) > 0.001f;

        // Convert input to camera-relative direction on XZ plane
        if (inputMoving)
        {
            Camera cam = null;
            if (cameraController != null && cameraController.gameObject != null)
            {
                cam = Camera.main; // prefer main camera
            }
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                Vector3 camForward = cam.transform.forward; camForward.y = 0f; camForward.Normalize();
                Vector3 camRight = cam.transform.right; camRight.y = 0f; camRight.Normalize();
                Vector3 camMove = camRight * moveInput.x + camForward * moveInput.z;
                moveInput = camMove.sqrMagnitude > 0.0001f ? camMove.normalized : Vector3.zero;
            }
            else
            {
                moveInput = moveInput.normalized;
            }
        }
        else
        {
            moveInput = Vector3.zero;
        }

        // Apply movement (only if will remain within moveRange)
        Vector3 prevPos = transform.position;
        Vector3 candidate = transform.position + moveInput * moveSpeed * Time.deltaTime;
        if (Vector3.Distance(originalPosition, candidate) <= moveRange)
        {
            transform.position = candidate;
        }

        // Update battle pos from actual transform
        unit.battlePos = new Vector2(transform.position.x, transform.position.z);

        // Compute actual planar speed from displacement this frame
        Vector3 delta = transform.position - prevPos;
        float actualPlanarSpeed = new Vector3(delta.x, 0f, delta.z).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        bool actuallyMoving = actualPlanarSpeed > 0.05f;

        // Footsteps based on actual movement
        if (actuallyMoving && !isMoving)
        {
            isMoving = true;
            if (sfxPlayer != null && !string.IsNullOrEmpty(stepLoopKey)) sfxPlayer.PlayLoop(stepLoopKey);
        }
        else if (!actuallyMoving && isMoving)
        {
            isMoving = false;
            if (sfxPlayer != null && !string.IsNullOrEmpty(stepLoopKey)) sfxPlayer.StopLoop(stepLoopKey);
        }

        // Face camera-relative input direction while there is input (WASD relative to camera)
        if (faceMoveDirection && inputMoving)
        {
            Vector3 faceDir = new Vector3(moveInput.x, 0f, moveInput.z);
            if (faceDir.sqrMagnitude > 0.0001f)
            {
                Transform rotT = visualRoot != null ? visualRoot : transform;
                Quaternion targetRot = Quaternion.LookRotation(faceDir.normalized, Vector3.up);
                rotT.rotation = Quaternion.Slerp(rotT.rotation, targetRot, Mathf.Clamp01(turnSpeed * Time.deltaTime));
            }
        }

        // Drive Animator from actual movement
        if (cachedAnimator != null)
        {
            float speedValue = normalizeAnimatorSpeed ? (actualPlanarSpeed / Mathf.Max(0.0001f, moveSpeed)) : actualPlanarSpeed;
            if (!actuallyMoving) speedValue = 0f;

            if (animHasSpeedParam && !string.IsNullOrEmpty(animatorSpeedParam))
            {
                cachedAnimator.SetFloat(animatorSpeedParam, speedValue * animatorSpeedScale);
            }

            // Direction/Turn relative to current forward and desired (camera-relative) input
            Vector3 desired = new Vector3(moveInput.x, 0f, moveInput.z);
            Transform rotT = visualRoot != null ? visualRoot : transform;
            Vector3 curFwd = rotT.forward; curFwd.y = 0f; if (curFwd.sqrMagnitude > 0f) curFwd.Normalize();

            if (animHasDirectionParam && !string.IsNullOrEmpty(animatorDirectionParam))
            {
                float dirVal = 0f;
                if (inputMoving && desired.sqrMagnitude > 0.0001f && curFwd.sqrMagnitude > 0.0001f)
                {
                    float ang = Vector3.SignedAngle(curFwd, desired.normalized, Vector3.up);
                    dirVal = Mathf.Clamp(ang / 120f, -1f, 1f);
                }
                cachedAnimator.SetFloat(animatorDirectionParam, dirVal);
            }

            if (animHasForwardParam && !string.IsNullOrEmpty(animatorForwardParam))
            {
                float forwardAmount = speedValue; // already normalized if option enabled
                if (!normalizeAnimatorSpeed)
                {
                    // if sending units/sec, clamp to [0,1] for Forward
                    forwardAmount = Mathf.Clamp01(forwardAmount / Mathf.Max(0.0001f, moveSpeed));
                }
                cachedAnimator.SetFloat(animatorForwardParam, forwardAmount * animatorSpeedScale);
            }

            if (animHasTurnParam && !string.IsNullOrEmpty(animatorTurnParam))
            {
                float turn = 0f;
                if (inputMoving && desired.sqrMagnitude > 0.0001f && curFwd.sqrMagnitude > 0.0001f)
                {
                    float ang = Vector3.SignedAngle(curFwd, desired.normalized, Vector3.up);
                    turn = Mathf.Clamp(ang / 180f, -1f, 1f);
                }
                cachedAnimator.SetFloat(animatorTurnParam, turn);
            }

            if (animHasGroundedParam && !string.IsNullOrEmpty(animatorGroundedParam))
            {
                cachedAnimator.SetBool(animatorGroundedParam, true);
            }
        }
    }

    // ===== 行动执行 =====

    protected virtual IEnumerator ExecuteAttack()
    {
        Debug.Log("[PlayerController] 执行普通攻击");

        // If a preselected target exists (from left click confirmation), use it directly
        if (preselectedTarget != null && skillSystem != null)
        {
            Debug.Log($"攻击目标(预选): {preselectedTarget.unitName}");
            skillSystem.CauseDamage(preselectedTarget, unit, unit.battleAtk, DamageType.Physics);
            if (sfxPlayer != null) sfxPlayer.Play("cut");
            // grant battle points on successful normal attack (same as non-preselected path)
            var tmQuick = FindObjectOfType<BattleTurnManager>();
            if (tmQuick != null)
            {
                tmQuick.AddBattlePoints(tmQuick.pointsPerNormalAttack);
            }
            preselectedTarget = null;
            yield return new WaitForSeconds(0.5f);
            yield break;
        }

        // otherwise fall back to interactive selection within melee range
        yield return SelectTarget(meleeRange);

        if (selectedTarget != null && skillSystem != null)
        {
            Debug.Log($"攻击目标: {selectedTarget.unitName}");
            skillSystem.CauseDamage(selectedTarget, unit, unit.battleAtk, DamageType.Physics);
            if (sfxPlayer != null) sfxPlayer.Play("cut");
            // 普攻成功后积攒战技点
            var tm = FindObjectOfType<BattleTurnManager>();
            if (tm != null)
            {
                tm.AddBattlePoints(tm.pointsPerNormalAttack);
            }
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
        // default implementation: if preselectedSkillIndex set, call ExecuteSkillByIndex
        if (preselectedSkillIndex >= 0)
        {
            int idx = preselectedSkillIndex;
            preselectedSkillIndex = -1;
            yield return ExecuteSkillByIndex(idx);
            yield break;
        }

        // 子类在此实现特定技能逻辑
        yield return new WaitForSeconds(0.3f);
    }

    // helper: execute skill by index (default: no-op, subclasses override if they support indexed skills)
    protected virtual IEnumerator ExecuteSkillByIndex(int index)
    {
        Debug.Log($"ExecuteSkillByIndex not implemented: {index}");
        yield return null;
    }

    // helper: return list of skill names for UI (subclasses override)
    protected virtual List<string> GetSkillNames()
    {
        return null;
    }
    // Optional: per-skill extra info like power rating and BP cost, shown next to the name
    protected virtual string GetSkillExtraInfo(int index)
    {
        return null;
    }

    // Build display strings for the UI by appending extra info to names
    protected virtual List<string> GetSkillDisplayStrings()
    {
        var names = GetSkillNames();
        if (names == null) return null;
        var list = new List<string>(names.Count);
        for (int i = 0; i < names.Count; i++)
        {
            string extra = GetSkillExtraInfo(i);
            if (!string.IsNullOrEmpty(extra)) list.Add($"{names[i]} ({extra})");
            else list.Add(names[i]);
        }
        return list;
    }

    // Called when skill list selection changes while hovering Skill.
    // Subclasses can override to show range indicators or highlight targets for the indexed skill.
    protected virtual void ShowSkillPreview(int index)
    {
        // default: no preview
    }

    // virtual: per-skill cast range used for the following circle; default to targetSelectionRange
    protected virtual float GetSkillCastRange(int index)
    {
        return targetSelectionRange;
    }

    // Try to execute selected skill immediately on single click in Skill mode.
    // Return true if executed (turn will proceed), false to keep selecting (e.g., no target or resource insufficient).
    protected virtual IEnumerator TryQuickCastSkill(int index)
    {
        //return false;
        yield return null;
    }

    // helper to test if a unit is a valid target
    protected bool IsValidTarget(BattleUnit u)
    {
        if (u == null || u == unit) return false;
        if (unit.unitType == BattleUnitType.Player && u.unitType == BattleUnitType.Enemy) return true;
        if (unit.unitType == BattleUnitType.Enemy && u.unitType == BattleUnitType.Player) return true;
        return false;
    }

    protected BattleUnit FindNearestValidTarget()
    {
        BattleUnit nearest = null;
        float bestDist = float.MaxValue;
        var units = FindObjectsOfType<BattleUnit>();
        foreach (var u in units)
        {
            if (!IsValidTarget(u)) continue;
            float d = Vector3.Distance(transform.position, u.transform.position);
            if (d <= targetSelectionRange && d < bestDist)
            {
                bestDist = d;
                nearest = u;
            }
        }
        return nearest;
    }

    /// <summary>
    /// 目标选择：在限定范围内选择一个敌方单位（默认使用 targetSelectionRange）
    /// </summary>
    protected IEnumerator SelectTarget()
    {
        yield return SelectTarget(targetSelectionRange);
    }

    /// <summary>
    /// 目标选择：在指定的 maxRange 内选择一个敌方单位
    /// </summary>
    protected IEnumerator SelectTarget(float maxRange)
    {
        currentSelectionMode = SelectionMode.TargetSelection;
        selectedTarget = null;

        if (battleUI != null)
            battleUI.UpdatePrompt("选择目标 (Tab切换/鼠标左键确认/Esc取消)");

        List<BattleUnit> validTargets = GetValidTargets(maxRange);
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

            // 鼠标左键确认：允许直接点击目标或确认当前高亮项
            if (Input.GetMouseButtonDown(0))
            {
                // if mouse clicked a unit under cursor, prefer it
                Camera cam2 = Camera.main ?? Camera.allCameras[0];
                if (cam2 != null)
                {
                    Ray rayClick = cam2.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(rayClick, out RaycastHit hitClick, 100f))
                    {
                        var clicked = hitClick.collider.GetComponentInParent<BattleUnit>();
                        if (clicked != null && IsValidTarget(clicked) && Vector3.Distance(transform.position, clicked.transform.position) <= maxRange)
                        {
                            selectedTarget = clicked;
                            currentSelectionMode = SelectionMode.None;
                            break;
                        }
                    }
                }

                // otherwise confirm current highlighted target
                currentSelectionMode = SelectionMode.None;
                break;
            }

            yield return null;
        }

        if (indicatorManager != null)
            indicatorManager.ClearAuxIndicators();
    }

    /// <summary>
    /// 区域选择：选择一个世界坐标作为技能释放中心
    /// </summary>
    protected IEnumerator SelectArea(float radius)
    {
        currentSelectionMode = SelectionMode.AreaSelection;
        selectedArea = transform.position;

        if (battleUI != null)
            battleUI.UpdatePrompt("选择区域 (鼠标移动/鼠标左键确认/Esc取消)");

        Camera cam = Camera.main ?? Camera.allCameras[0];
        if (cam == null)
        {
            Debug.LogWarning("未找到摄像机，无法选择区域");
            currentSelectionMode = SelectionMode.None;
            yield break;
        }

        while (currentSelectionMode == SelectionMode.AreaSelection)
        {
            // 鼠标射线检测地面 (忽略单位碰撞体)
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (RaycastIgnoreUnits(ray, out RaycastHit hit, 100f))
            {
                Vector3 hitPos = hit.point;

                // 限制在范围内
                if (Vector3.Distance(transform.position, hitPos) <= targetSelectionRange)
                {
                    selectedArea = hitPos;
                    if (indicatorManager != null)
                    {
                        // 使用 AreaSelection 标签，自动清理同标签旧指示器，确保只显示一个
                        indicatorManager.CreateCircleIndicator(
                        hitPos,
                        radius,
                        true,
                        false,
                        BattleIndicatorManager.Tags.AreaSelection,
                        true
                        );
                    }
                }
                else
                {
                    if (indicatorManager != null)
                    {
                        indicatorManager.CreateCircleIndicator(
                        hitPos,
                        radius,
                        false,
                        false,
                        BattleIndicatorManager.Tags.AreaSelection,
                        true
                        );
                    }
                }
            }

            // 鼠标左键确认
            if (Input.GetMouseButtonDown(0))
            {
                currentSelectionMode = SelectionMode.None;
                // 确认后清理区域选择指示器
                if (indicatorManager != null)
                    indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.AreaSelection);
                break;
            }

            // Esc取消
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                currentSelectionMode = SelectionMode.None;
                //取消时也清理区域选择指示器
                if (indicatorManager != null)
                    indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.AreaSelection);
                break;
            }

            yield return null;
        }

        //兜底清理（防止异常路径遗留）
        if (indicatorManager != null)
            indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.AreaSelection);
    }

    /// <summary>
    /// 方向选择：选择镜头朝向作为技能方向
    /// </summary>
    protected IEnumerator SelectDirection()
    {
        currentSelectionMode = SelectionMode.DirectionSelection;
        GameObject sectorIndicator = null;

        if (battleUI != null)
            battleUI.UpdatePrompt("调整方向 (鼠标/鼠标左键确认/Esc取消)");

        Camera cam = Camera.main ?? Camera.allCameras[0];
        if (cam == null)
        {
            selectedDirection = transform.forward;
            currentSelectionMode = SelectionMode.None;
            yield break;
        }

        // 创建扇形指示器（DirectionSelection标签，确保唯一）
        if (indicatorManager != null)
            sectorIndicator = indicatorManager.CreateSectorIndicator(transform, targetSelectionRange, sectorAngle, BattleIndicatorManager.Tags.DirectionSelection, true);

        while (currentSelectionMode == SelectionMode.DirectionSelection)
        {
            // 获取镜头前向（投影到水平面）
            Vector3 camForward = cam.transform.forward;
            camForward.y = 0f;
            if (camForward.sqrMagnitude > 0.01f)
            {
                selectedDirection = camForward.normalized;

                // 更新扇形指示器朝向
                if (sectorIndicator != null && indicatorManager != null)
                    indicatorManager.UpdateSectorRotation(sectorIndicator, transform, selectedDirection);
            }

            // 鼠标左键确认
            if (Input.GetMouseButtonDown(0))
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

        // 清理方向选择指示器
        if (indicatorManager != null)
            indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.DirectionSelection);
    }

    // ===== 辅助方法 =====

    /// <summary>
    /// 获取有效目标列表（敌方单位，在范围内）
    /// </summary>
    protected List<BattleUnit> GetValidTargets()
    {
        return GetValidTargets(targetSelectionRange);
    }

    /// <summary>
    /// 获取有效目标列表（指定最大范围）
    /// </summary>
    protected List<BattleUnit> GetValidTargets(float maxRange)
    {
        List<BattleUnit> targets = new List<BattleUnit>();
        BattleUnit[] allUnits = FindObjectsOfType<BattleUnit>();

        foreach (var u in allUnits)
        {
            if (u == null || u == unit) continue;

            //只选择敌方单位
            if (unit.unitType == BattleUnitType.Player && u.unitType == BattleUnitType.Enemy)
            {
                // 检查距离
                if (Vector3.Distance(transform.position, u.transform.position) <= maxRange)
                {
                    targets.Add(u);
                }
            }
            else if (unit.unitType == BattleUnitType.Enemy && u.unitType == BattleUnitType.Player)
            {
                if (Vector3.Distance(transform.position, u.transform.position) <= maxRange)
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
    /// 供子类调用：使用目标选择（自定义最大范围）
    /// </summary>
    protected IEnumerator UseTargetSelection(float maxRange, System.Action<BattleUnit> onTargetSelected)
    {
        yield return SelectTarget(maxRange);
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

    // 判断技能是否为单体目标技能（子类可重写）
    protected virtual bool IsSkillSingleTarget(int skillIndex)
    {
        return false; // 默认无单体技能
    }

    // helper to keep movement indicator grounded
    protected Vector3 GetGroundPosition(Vector3 source)
    {
        // raycast down from above to find ground; ignore hits on units
        Vector3 start = source + Vector3.up * 5f;
        Ray down = new Ray(start, Vector3.down);
        if (RaycastIgnoreUnits(down, out RaycastHit h, 20f))
        {
            return h.point;
        }
        // fallback: project to y =0
        return new Vector3(source.x, 0f, source.z);
    }

    // Raycast helper that ignores colliders belonging to BattleUnit (and their parents).
    // Returns the closest hit that is not part of a BattleUnit, or false if none.
    protected bool RaycastIgnoreUnits(Ray ray, out RaycastHit outHit, float maxDistance)
    {
        outHit = default;
        var hits = Physics.RaycastAll(ray, maxDistance);
        if (hits == null || hits.Length == 0) return false;
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            // skip if this collider is on a BattleUnit
            if (h.collider.GetComponentInParent<BattleUnit>() != null) continue;
            outHit = h;
            return true;
        }
        return false;
    }

    // Map animator parameter names to what's actually on the controller (Unity-Chan / Standard Assets / custom)
    private void MapAnimatorParameters()
    {
        animHasSpeedParam = animHasDirectionParam = animHasGroundedParam = false;
        animHasForwardParam = animHasTurnParam = false;
        if (cachedAnimator == null) return;

        // Candidates arrays
        string[] speedCandidates = new[] { animatorSpeedParam, "Speed", "Forward", "MoveSpeed" };
        string[] dirCandidates = new[] { animatorDirectionParam, "Direction", "TurnSpeed", "AngularSpeed" };
        string[] forwardCandidates = new[] { animatorForwardParam, "Forward", "Speed" };
        string[] turnCandidates = new[] { animatorTurnParam, "Turn", "Direction" };
        string[] groundedCandidates = new[] { animatorGroundedParam, "Grounded", "isGround", "IsGround", "IsGrounded" };

        // Scan parameters once
        var ps = cachedAnimator.parameters;
        System.Func<string[], AnimatorControllerParameterType, string> find = (cands, type) =>
        {
            foreach (var c in cands)
            {
                if (string.IsNullOrEmpty(c)) continue;
                foreach (var p in ps)
                {
                    if (p.type == type && p.name == c) return c;
                }
            }
            return null;
        };

        var sp = find(speedCandidates, AnimatorControllerParameterType.Float);
        if (!string.IsNullOrEmpty(sp)) { animatorSpeedParam = sp; animHasSpeedParam = true; }

        var dp = find(dirCandidates, AnimatorControllerParameterType.Float);
        if (!string.IsNullOrEmpty(dp)) { animatorDirectionParam = dp; animHasDirectionParam = true; }

        var fp = find(forwardCandidates, AnimatorControllerParameterType.Float);
        if (!string.IsNullOrEmpty(fp)) { animatorForwardParam = fp; animHasForwardParam = true; }

        var tp = find(turnCandidates, AnimatorControllerParameterType.Float);
        if (!string.IsNullOrEmpty(tp)) { animatorTurnParam = tp; animHasTurnParam = true; }

        var gp = find(groundedCandidates, AnimatorControllerParameterType.Bool);
        if (!string.IsNullOrEmpty(gp)) { animatorGroundedParam = gp; animHasGroundedParam = true; }

        // Ensure root motion doesn't block scripted move
        cachedAnimator.applyRootMotion = false;
    }

    // ===== 连携相关 =====

    // ===== helper methods for chain =====
    private BattleUnit FindBestChainPartner()
    {
        var all = FindObjectsOfType<BattleUnit>();
        BattleUnit best = null;
        float bestDist = float.MaxValue;
        foreach (var u in all)
        {
            if (u == null || u == unit) continue;
            if (u.unitType != unit.unitType) continue; // only allies
            if (u.battleHp <= 0) continue;
            float d = Vector3.Distance(transform.position, u.transform.position);
            if (d <= chainRange * 2f) // 环相交即可连携
            {
                if (d < bestDist)
                {
                    bestDist = d;
                    best = u;
                }
            }
        }
        return best;
    }

    private void UpdateChainVisuals(BattleUnit partner)
    {
        if (indicatorManager == null)
        {
            chainCircleSelf = null;
            chainCircleAlly = null;
            return;
        }
        if (partner == null)
        {
            // 清理连携圈
            indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.ChainCircle);
            chainCircleSelf = null;
            chainCircleAlly = null;
            return;
        }
        // 自己圈
        Vector3 selfPos = GetGroundPosition(transform.position);
        if (chainCircleSelf == null)
        {
            chainCircleSelf = indicatorManager.CreateChainCircle(selfPos, chainRange, true, false);
        }
        else
        {
            indicatorManager.UpdateCircleIndicatorKeepColor(chainCircleSelf, selfPos, chainRange);
        }
        //伙伴圈
        Vector3 allyPos = GetGroundPosition(partner.transform.position);
        if (chainCircleAlly == null)
        {
            chainCircleAlly = indicatorManager.CreateChainCircle(allyPos, chainRange, true, false);
        }
        else
        {
            indicatorManager.UpdateCircleIndicatorKeepColor(chainCircleAlly, allyPos, chainRange);
        }
    }

    private IEnumerator PerformChainAttack(BattleUnit partner, BattleUnit target)
    {
        if (partner == null || target == null || skillSystem == null) yield break;
        // 移动到普攻范围内
        float partnerRange = meleeRange;
        float partnerSpeed = moveSpeed;
        var pc = partner.controller as PlayerController;
        if (pc != null)
        {
            partnerRange = pc.meleeRange;
            partnerSpeed = pc.moveSpeed;
        }
        // 简单直线移动，直到进入范围或超过安全步数
        int safe = 0;
        while (Vector3.Distance(partner.transform.position, target.transform.position) > partnerRange && safe < 600)
        {
            safe++;
            Vector3 dir = (target.transform.position - partner.transform.position);
            dir.y = 0f;
            float dist = dir.magnitude;
            if (dist < 0.001f) break;
            Vector3 step = dir.normalized * partnerSpeed * Time.deltaTime;
            if (step.magnitude > dist) step = dir.normalized * dist;
            partner.transform.position += step;

            // 播放伙伴移动动画（根据实际步进速度驱动Speed参数）
            if (pc != null && pc.cachedAnimator != null && pc.animHasSpeedParam && !string.IsNullOrEmpty(pc.animatorSpeedParam))
            {
                float actualPlanarSpeed = step.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                float speedVal = pc.normalizeAnimatorSpeed ? (actualPlanarSpeed / Mathf.Max(0.0001f, partnerSpeed)) : actualPlanarSpeed;
                pc.cachedAnimator.SetFloat(pc.animatorSpeedParam, speedVal * pc.animatorSpeedScale);
            }
            yield return null;
        }
        // 停止伙伴移动动画
        if (pc != null && pc.cachedAnimator != null && pc.animHasSpeedParam && !string.IsNullOrEmpty(pc.animatorSpeedParam))
        {
            pc.cachedAnimator.SetFloat(pc.animatorSpeedParam, 0f);
        }
        // 执行一次普攻伤害与音效、积攒战技点
        if (target.battleHp > 0)
        {
            skillSystem.CauseDamage(target, partner, partner.battleAtk, DamageType.Physics);
            if (sfxPlayer != null) sfxPlayer.Play("cut");
            var tm = FindObjectOfType<BattleTurnManager>();
            if (tm != null)
            {
                tm.AddBattlePoints(tm.pointsPerNormalAttack);
            }
        }
        // 再次确保动画停止
        if (pc != null && pc.cachedAnimator != null && pc.animHasSpeedParam && !string.IsNullOrEmpty(pc.animatorSpeedParam))
        {
            pc.cachedAnimator.SetFloat(pc.animatorSpeedParam, 0f);
        }
        yield return new WaitForSeconds(0.2f);
    }

    private void ResetMovementState()
    {
        isMoving = false;
        if (sfxPlayer != null && !string.IsNullOrEmpty(stepLoopKey)) sfxPlayer.StopLoop(stepLoopKey);
        if (cachedAnimator != null && animHasSpeedParam && !string.IsNullOrEmpty(animatorSpeedParam))
        {
            cachedAnimator.SetFloat(animatorSpeedParam, 0f);
        }
    }
}
