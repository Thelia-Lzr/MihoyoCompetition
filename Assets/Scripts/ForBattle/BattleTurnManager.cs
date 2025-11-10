using Assets.Scripts.ForBattle;
using Assets.Scripts.ForBattle.Indicators;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System; // 为事件 Action
using UnityEngine.SceneManagement; // 场景跳转
using Assets.Scripts.ForBattle.Barriers; //结界系统
using Assets.Scripts.ForBattle.Audio;

public class BattleTurnManager : MonoBehaviour
{
    public BattleUnitPriorityQueue turnOrder;
    public SkillSystem skillSystem;
    // Camera controller to handle Cinemachine-based camera switching during turns
    public BattleCameraController cameraController;
    // indicator manager to show movement/skill ranges
    public BattleIndicatorManager indicatorManager;
    //public SceneManager sceneManager;
    public enum BattleStatus { Idle, Battle }
    public BattleStatus status = BattleStatus.Idle;

    [Header("Battle Skill Points (战技点)")]
    [Tooltip("当前战技点（全队共享）")]
    public int battlePoints =0;
    [Tooltip("战技点上限")]
    public int battlePointsMax =10;
    [Tooltip("普通攻击每次获得的战技点")]
    public int pointsPerNormalAttack =1;

    [Header("UI")]
    [Tooltip("用于显示战技点的 TextMeshPro 文本对象 (可选)")]
    public TMP_Text battlePointsText;

    [Header("Turn Settings")]
    public int actionPointThreshold =100;
    public float tickInterval =0.1f; // seconds per AP accumulation tick

    [Header("Camera Focus Settings")]
    [Tooltip("当多个单位同时达到行动阈值时，只对第一个单位执行镜头平滑聚焦，后续单位复用该视角，避免快速抖动。")] public bool focusFirstReadyUnitOnlyPerBatch = true;
    [Tooltip("始终确保玩家单位开始行动时获得镜头焦点，即使本批次已有其他单位获取过焦点。")] public bool alwaysFocusPlayerUnits = true;

    private Coroutine turnCoroutine = null;

    // track previous unit to enable smooth transition
    private BattleUnit previousUnit = null;

    // Optional per-unit initial act point to assign after they finish a turn (used to grant immediate extra action)
    private class InitialActInfo { public int actPoint; public bool isExtra; }
    private Dictionary<BattleUnit, InitialActInfo> initialActPointForUnit = new Dictionary<BattleUnit, InitialActInfo>();
    // Tracks units whose next turn is marked as an "extra" turn (won't grant auto BP on start)
    private HashSet<BattleUnit> extraTurnPending = new HashSet<BattleUnit>();
    // Tracks units that are currently executing an extra turn (should not decrement barrier durations)
    private HashSet<BattleUnit> activeExtraTurns = new HashSet<BattleUnit>();

    // ===== 行动条事件 =====
    /// <summary>
    /// 在批量推进行动点后触发，用于行动条0.5s动画。
    /// prevPoints/newPoints: 单位->行动点值映射；duration: 动画时长（固定0.5s）
    /// </summary>
    public event Action<Dictionary<BattleUnit, int>, Dictionary<BattleUnit, int>, float> OnActionBarAdvance;
    /// <summary>
    /// 单位开始执行回合（达到行动阈值、进入操作阶段）
    /// </summary>
    public event Action<BattleUnit> OnUnitTurnStart;
    /// <summary>
    /// 单位结束执行回合（行动点已扣除）
    /// </summary>
    public event Action<BattleUnit> OnUnitTurnEnd;

    public BattleUnit actingUnit; // 当前正在执行回合的单位（供结界影响BP消耗）

    public void StartBattle()
    {
        this.status = BattleStatus.Battle;
        battlePoints =0;
        UpdateBattlePointsUI();
        // play battle BGM loop when battle starts
        if (SfxPlayer.Instance != null)
        {
            SfxPlayer.Instance.PlayLoop("battle1");
        }
    }
    public void EndBattle()
    {
        this.status = BattleStatus.Idle;
        // stop battle BGM when battle ends
        if (SfxPlayer.Instance != null)
        {
            SfxPlayer.Instance.StopLoop("battle1");
        }
    }
    // Start is called before the first frame update
    public void AddUnitToTurnOrder(BattleUnit unit)
    {
        if (unit == null) return;
        // Do not add units that are not currently on battle (on bench)
        var pc = unit.controller as PlayerController;
        if (pc != null && !pc.isOnBattle) return;
        if (turnOrder == null) turnOrder = new BattleUnitPriorityQueue();
        turnOrder.Add(unit);
    }

    void Start()
    {
        if (turnOrder == null) turnOrder = new BattleUnitPriorityQueue();

        if (indicatorManager == null)
        {
            // try to auto-find manager to avoid null reference
            indicatorManager = FindObjectOfType<BattleIndicatorManager>();
            if (indicatorManager != null)
            {
                Debug.Log("BattleTurnManager: Auto-found BattleIndicatorManager: " + indicatorManager.name);
            }
            else
            {
                Debug.LogWarning("BattleTurnManager: indicatorManager is not assigned and none found in scene.");
            }
        }

        // 初始化战技点显示
        UpdateBattlePointsUI();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// Fast-forward AP by computing the minimum number of ticks needed for any unit to reach the threshold
    /// and adding per-tick increments in bulk to all units. Returns the number of ticks applied.
    /// </summary>
    private int FastForwardTicksToNextAction()
    {
        if (turnOrder == null || turnOrder.Count == 0) return 0;

        // Ensure dead units are removed/destroyed before computing ticks
        RemoveDeadUnits();

        int minTicks = int.MaxValue;
        var units = turnOrder.GetAll();
        foreach (var u in units)
        {
            if (u == null) continue;
            int perTick = Mathf.Max(1, Mathf.RoundToInt(u.battleSpd * tickInterval));
            if (u.battleActPoint >= actionPointThreshold)
            {
                minTicks = 0;
                break;
            }
            int need = Mathf.CeilToInt((actionPointThreshold - u.battleActPoint) / (float)perTick);
            if (need < minTicks) minTicks = need;
        }

        if (minTicks == int.MaxValue || minTicks <= 0) return 0;

        // apply bulk increment
        foreach (var u in units)
        {
            if (u == null) continue;
            int perTick = Mathf.Max(1, Mathf.RoundToInt(u.battleSpd * tickInterval));
            u.battleActPoint += perTick * minTicks;
        }

        // resort after bulk update
        turnOrder.Sort();
        return minTicks;
    }

    void Awake() { DontDestroyOnLoad(gameObject); }
    IEnumerator ProcessTurn()
    {
        // Main loop runs while status == Battle
        while (status == BattleStatus.Battle)
        {
            if (turnOrder == null || turnOrder.Count ==0)
            {
                // nothing to do
                yield return null;
                continue;
            }

            //记录推进前行动点
            Dictionary<BattleUnit, int> prevPoints = new Dictionary<BattleUnit, int>();
            foreach (var u in turnOrder.GetAll()) if (u != null) prevPoints[u] = u.battleActPoint;

            int appliedTicks = FastForwardTicksToNextAction();
            if (appliedTicks ==0)
            {
                // 若没有推进（已有单位就绪），不等待动画直接处理
            }
            else
            {
                // 推进后记录新值并播放0.5s 动画
                Dictionary<BattleUnit, int> newPoints = new Dictionary<BattleUnit, int>();
                foreach (var u in turnOrder.GetAll()) if (u != null) newPoints[u] = u.battleActPoint;
                OnActionBarAdvance?.Invoke(prevPoints, newPoints,0.5f);
                yield return new WaitForSeconds(0.5f);
            }

            //可能有多个单位同时达到阈值，依次处理全部，避免玩家回合被跳过
            bool processedAny = false;
            bool cameraFocusedThisBatch = false; // 防抖：同一批只聚焦一次（可被玩家单位覆盖）
            while (turnOrder.Count > 0)
            {
                BattleUnit currentUnit = turnOrder.Peek();
                if (currentUnit == null || currentUnit.battleActPoint < actionPointThreshold) break; // 无更多就绪单位

                actingUnit = currentUnit; // 设置当前行动单位（用于结界BP减免）

                processedAny = true;
                currentUnit.Flush();
                // Grant BP on player turn start unless this turn was flagged as an extra turn
                if (currentUnit.unitType == BattleUnitType.Player)
                {
                    if (extraTurnPending.Contains(currentUnit))
                    {
                        // consume the extra-turn flag and do not award BP
                        extraTurnPending.Remove(currentUnit);
                        // mark this unit as currently executing an extra turn so barriers don't consume a duration
                        activeExtraTurns.Add(currentUnit);
                    }
                    else
                    {
                        battlePoints = Mathf.Min(battlePoints + 1, battlePointsMax);
                    }
                }
                UpdateBattlePointsUI();

                Debug.Log("当前行动单位: " + currentUnit.unitName);

                if (skillSystem != null) skillSystem.OnUnitTurnStart(currentUnit);
                OnUnitTurnStart?.Invoke(currentUnit);

                // 显示移动范围
                if (indicatorManager != null)
                {
                    float moveRange = 0f;
                    var pc = currentUnit.controller as PlayerController;
                    moveRange = pc != null ? pc.moveRange : 3f;
                    Vector3 origin = currentUnit.transform.position + Vector3.up * 0.5f;
                    RaycastHit hit;
                    Vector3 circleCenter = Physics.Raycast(origin, Vector3.down, out hit, 5f)
                        ? hit.point + Vector3.up * 0.01f
                        : new Vector3(currentUnit.transform.position.x, 0f + 0.01f, currentUnit.transform.position.z);
                    indicatorManager.CreateCircleIndicator(circleCenter, moveRange, true, true, BattleIndicatorManager.Tags.MovementRange, true);
                }

                // 相机与控制器执行
                if (cameraController != null)
                {
                    bool allowFocus = !focusFirstReadyUnitOnlyPerBatch || !cameraFocusedThisBatch || (alwaysFocusPlayerUnits && currentUnit.unitType == BattleUnitType.Player);
                    if (allowFocus)
                    {
                        Transform toT = currentUnit.cameraRoot != null ? currentUnit.cameraRoot : currentUnit.transform;
                        if (previousUnit != null && previousUnit != currentUnit)
                        {
                            // 使用立即聚焦，减少旧目标残留；如果需要平滑过渡可改回 Transition
                            cameraController.ForceFocusTarget(toT, true);
                        }
                        else
                        {
                            cameraController.ForceFocusTarget(toT, true);
                            yield return new WaitForSeconds(cameraController.switchTransitionTime * 0.25f);
                        }
                        // 标记本批次已聚焦（玩家单位强制聚焦不影响后续玩家再次聚焦，因为条件中包含 alwaysFocusPlayerUnits）
                        cameraFocusedThisBatch = true;
                    }

                    cameraController.EnableMouseControl(true);
                    if (currentUnit.controller != null)
                    {
                        yield return StartCoroutine(currentUnit.controller.ExecuteTurn(this));
                    }
                    else
                    {
                        Debug.Log("按空格继续到下一个单位");
                        yield return StartCoroutine(WaitForSpaceKey());
                    }
                    cameraController.EnableMouseControl(false);
                }
                else
                {
                    if (currentUnit.controller != null)
                        yield return StartCoroutine(currentUnit.controller.ExecuteTurn(this));
                    else
                        yield return new WaitForSeconds(0.3f);
                }

                // 扣除行动点并触发结束事件
                currentUnit.battleActPoint = Math.Max(0, currentUnit.battleActPoint - actionPointThreshold);
                OnUnitTurnEnd?.Invoke(currentUnit);

                //结界回合结束结算
                foreach (var barrier in BarrierBase.ActiveBarriers)
                {
                    if (barrier == null) continue;
                    barrier.ForceTurnEndResolve(currentUnit);
                }

                // 如果有为该单位预设的初始行动点（例如释放结界后），应用它以便可能立即再次行动
                if (initialActPointForUnit.TryGetValue(currentUnit, out InitialActInfo initInfo))
                {
                    currentUnit.battleActPoint = initInfo.actPoint;
                    // 标记为额外回合的单位在下一回合开始时将不会获得自动BP
                    if (initInfo.isExtra) extraTurnPending.Add(currentUnit);
                    initialActPointForUnit.Remove(currentUnit);
                }

                // 清理临时指示
                if (indicatorManager != null)
                {
                    indicatorManager.ClearEphemeralIndicators();
                }
                // Clear active extra turn marker for this unit if it was set
                if (activeExtraTurns.Contains(currentUnit)) activeExtraTurns.Remove(currentUnit);

                turnOrder.Sort();
                previousUnit = currentUnit;
                actingUnit = null; // 回合结束清空
            }

            // 若没有任何单位处理，做一个最小等待避免死循环占用 CPU
            if (!processedAny) yield return new WaitForSeconds(tickInterval);

            // ===== 回合循环末尾：统一清理死亡单位（跳过不在战斗的己方备战角色） =====
            RemoveDeadUnits();

            // ===== 胜利检测：无敌方单位则结束战斗并跳回 Scenario 场景 =====
            bool anyEnemy = false;
            foreach (var u in GameObject.FindObjectsOfType<BattleUnit>())
            {
                if (u != null && u.unitType == BattleUnitType.Enemy) { anyEnemy = true; break; }
            }
            if (!anyEnemy)
            {
                Debug.Log("玩家胜利！返回 Scenario 场景。");
                EndBattle();
                status = BattleStatus.Idle;
                if (indicatorManager != null) indicatorManager.ClearAll();
                if (Application.isPlaying)
                {
                    SfxPlayer.Instance.StopLoop("battle1");
                    try { global::UnityEngine.SceneManagement.SceneManager.LoadScene("Scenario"); }
                    catch (System.Exception ex) { Debug.LogWarning("加载 Scenario 场景失败: " + ex.Message); }
                }
                break; //退出循环
            }

            // 失败检测：仅检测场上（isOnBattle==true）的玩家单位是否全部阵亡。若场上所有上阵玩家血量均为0，则判定战斗失败
            bool anyPlayerAlive = false;
            var pcs = GameObject.FindObjectsOfType<PlayerController>();
            foreach (var pc in pcs)
            {
                if (pc == null) continue;
                if (pc.unit == null) continue;
                // only consider units that are currently on-battle
                if (!pc.isOnBattle) continue;
                if (pc.unit.battleHp > 0)
                {
                    anyPlayerAlive = true;
                    break;
                }
            }
            if (!anyPlayerAlive)
            {
                Debug.Log("所有玩家单位阵亡，战斗失败，返回 Scenario 场景。");
                EndBattle();
                status = BattleStatus.Idle;
                if (indicatorManager != null) indicatorManager.ClearAll();
                if (Application.isPlaying)
                {
                    SfxPlayer.Instance.StopLoop("battle1");
                    try { global::UnityEngine.SceneManagement.SceneManager.LoadScene("Scenario"); }
                    catch (System.Exception ex) { Debug.LogWarning("加载 Scenario 场景失败: " + ex.Message); }
                }
                break; //退出循环
            }
        }

        // Cleanup when exiting battle
        if (cameraController != null) cameraController.ShowOverview();
        turnCoroutine = null;
        actingUnit = null;
    }

    IEnumerator WaitForSpaceKey()
    {
        while (status == BattleStatus.Battle)
        {
            if (Input.GetKeyDown(KeyCode.Space)) yield break;
            yield return null;
        }
    }

    public void Flush()
    {
        if (turnOrder != null) turnOrder.Clear();
    }

    // Allow external callers (e.g. CeliaController) to set an initial action point for a unit
    // that will be applied immediately after the unit's current turn ends, enabling an immediate extra action.
    // If isExtra==true the next turn taken by the unit will be considered an "extra turn" and will not grant auto BP.
    public void SetInitialActPointForUnit(BattleUnit unit, int actPointValue, bool isExtra = false)
    {
        if (unit == null) return;
        var info = new InitialActInfo { actPoint = actPointValue, isExtra = isExtra };
        initialActPointForUnit[unit] = info;
    }

    // Centralized removal of dead units: remove from turnOrder, invoke EndBattle and destroy GameObject
    private void RemoveDeadUnits()
    {
        var all = GameObject.FindObjectsOfType<BattleUnit>();
        if (all == null || all.Length == 0) return;
        foreach (var u in all)
        {
            if (u == null) continue;
            if (u.battleHp <= 0)
            {
                // Skip bench (not on-battle) player characters to avoid removing uninitialized off-battle units
                var pc = u.controller as PlayerController;
                if (pc != null && !pc.isOnBattle) continue;

                if (turnOrder != null) turnOrder.Remove(u);
                try
                {
                    u.EndBattle();
                }
                catch { }
                // Destroy the GameObject to ensure it no longer participates
                if (u.gameObject != null)
                {
                    Destroy(u.gameObject);
                }
            }
        }
        if (turnOrder != null) turnOrder.Sort();
    }

    // Update is called once per frame
    void Update()
    {
        // start/stop the turn coroutine based on status
        if (status == BattleStatus.Battle)
        {
            if (turnCoroutine == null)
            {
                previousUnit = null;
                turnCoroutine = StartCoroutine(ProcessTurn());
            }
        }
        else
        {
            if (turnCoroutine != null)
            {
                StopCoroutine(turnCoroutine);
                turnCoroutine = null;
                if (cameraController != null)
                {
                    cameraController.EnableMouseControl(false);
                    cameraController.ShowOverview();
                }
            }
        }
    }

    // ===== 战技点（Battle Points）API =====
    private int GetAdjustedCost(int amount)
    {
        if (actingUnit == null) return amount;
        int barrierReduction = BarrierBase.GetTotalBpCostDeltaForUnit(actingUnit);
        int adjusted = Mathf.Max(0, amount - barrierReduction);
        return adjusted;
    }
    public void ResetBattlePoints()
    {
        battlePoints =0;
        UpdateBattlePointsUI();
    }

    public int GetBattlePoints()
    {
        return battlePoints;
    }

    public void AddBattlePoints(int amount)
    {
        if (amount <=0) return;
        int before = battlePoints;
        battlePoints = Mathf.Clamp(battlePoints + amount,0, battlePointsMax);
        Debug.Log($"[BattleTurnManager] 战技点 +{amount}: {before} -> {battlePoints}/{battlePointsMax}");
        UpdateBattlePointsUI();
    }

    public bool CanSpendBattlePoints(int amount)
    {
        int adj = GetAdjustedCost(amount);
        return adj >=0 && battlePoints >= adj;
    }
    public bool TrySpendBattlePoints(int amount)
    {
        int adj = GetAdjustedCost(amount);
        if (battlePoints < adj) return false;
        battlePoints -= adj;
        Debug.Log($"[BattleTurnManager] 战技点 消耗 {adj}(原始:{amount})，剩余 {battlePoints}/{battlePointsMax}");
        UpdateBattlePointsUI();
        return true;
    }
    /// <summary>
    /// Returns true if the provided unit is currently executing an extra turn (i.e., its turn was scheduled
    /// via SetInitialActPointForUnit with isExtra==true and has not yet completed). Used by barriers to skip
    /// decrementing duration during extra turns.
    /// </summary>
    public bool IsActiveExtraTurn(BattleUnit unit)
    {
        if (unit == null) return false;
        return activeExtraTurns != null && activeExtraTurns.Contains(unit);
    }
    private void UpdateBattlePointsUI()
    {
        if (battlePointsText == null) return;
        battlePointsText.text = $"当前战技点：{battlePoints}/{battlePointsMax}";
    }
}
