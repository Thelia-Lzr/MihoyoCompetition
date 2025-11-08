using Assets.Scripts.ForBattle;
using Assets.Scripts.ForBattle.Indicators;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System; // 为事件 Action
using UnityEngine.SceneManagement; // 场景跳转
using Assets.Scripts.ForBattle.Barriers; //结界系统

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

    private Coroutine turnCoroutine = null;

    // track previous unit to enable smooth transition
    private BattleUnit previousUnit = null;

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
    }
    public void EndBattle()
    {
        this.status = BattleStatus.Idle;
    }
    // Start is called before the first frame update
    public void AddUnitToTurnOrder(BattleUnit unit)
    {
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
        if (turnOrder == null || turnOrder.Count ==0) return 0;

        int minTicks = int.MaxValue;
        var units = turnOrder.GetAll();
        foreach (var u in units)
        {
            if(u.battleHp <=0)
            {
                u.EndBattle();
                turnOrder.Remove(u);
            }
            if (u == null) continue;
            int perTick = Mathf.Max(1, Mathf.RoundToInt(u.battleSpd * tickInterval));
            if (u.battleActPoint >= actionPointThreshold)
            {
                minTicks =0;
                break;
            }
            int need = Mathf.CeilToInt((actionPointThreshold - u.battleActPoint) / (float)perTick);
            if (need < minTicks) minTicks = need;
        }

        if (minTicks == int.MaxValue || minTicks <=0) return 0;

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
            while (turnOrder.Count >0)
            {
                BattleUnit currentUnit = turnOrder.Peek();
                if (currentUnit == null || currentUnit.battleActPoint < actionPointThreshold) break; // 无更多就绪单位

                actingUnit = currentUnit; // 设置当前行动单位（用于结界BP减免）

                processedAny = true;
                currentUnit.Flush();
                if (currentUnit.unitType == BattleUnitType.Player) battlePoints +=1;
                UpdateBattlePointsUI();

                Debug.Log("当前行动单位: " + currentUnit.unitName);

                if (skillSystem != null) skillSystem.OnUnitTurnStart(currentUnit);
                OnUnitTurnStart?.Invoke(currentUnit);

                // 显示移动范围
                if (indicatorManager != null)
                {
                    float moveRange =0f;
                    var pc = currentUnit.controller as PlayerController;
                    moveRange = pc != null ? pc.moveRange :3f;
                    Vector3 origin = currentUnit.transform.position + Vector3.up *0.5f;
                    RaycastHit hit;
                    Vector3 circleCenter = Physics.Raycast(origin, Vector3.down, out hit,5f)
                        ? hit.point + Vector3.up *0.01f
                        : new Vector3(currentUnit.transform.position.x,0f +0.01f, currentUnit.transform.position.z);
                    indicatorManager.CreateCircleIndicator(circleCenter, moveRange, true, true, BattleIndicatorManager.Tags.MovementRange, true);
                }

                // 相机与控制器执行
                if (cameraController != null)
                {
                    Vector3 desiredOffset = new Vector3(0f,1.2f, -3f);
                    if (previousUnit != null && previousUnit != currentUnit)
                    {
                        Transform fromT = previousUnit.cameraRoot != null ? previousUnit.cameraRoot : previousUnit.transform;
                        Transform toT = currentUnit.cameraRoot != null ? currentUnit.cameraRoot : currentUnit.transform;
                        yield return StartCoroutine(cameraController.TransitionToTarget(fromT, toT, desiredOffset, cameraController.transitionDuration));
                    }
                    else
                    {
                        Transform toT = currentUnit.cameraRoot != null ? currentUnit.cameraRoot : currentUnit.transform;
                        cameraController.FocusImmediate(toT, desiredOffset);
                        yield return new WaitForSeconds(cameraController.blendTime);
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
                currentUnit.battleActPoint = Mathf.Max(0, currentUnit.battleActPoint - actionPointThreshold);
                OnUnitTurnEnd?.Invoke(currentUnit);

                //触发所有结界的“回合结束结算”
                foreach (var barrier in BarrierBase.ActiveBarriers)
                {
                    if (barrier == null) continue;
                    barrier.ForceTurnEndResolve(currentUnit);
                }

                // 清理临时指示与排序（保留结界指示器）
                if (indicatorManager != null)
                {
                    if (indicatorManager != null) indicatorManager.ClearEphemeralIndicators();
                }
                turnOrder.Sort();
                previousUnit = currentUnit;
                actingUnit = null; // 回合结束清空
            }

            // 若没有任何单位处理，做一个最小等待避免死循环占用 CPU
            if (!processedAny) yield return new WaitForSeconds(tickInterval);

            // ===== 回合循环末尾：清理死亡单位 =====
            // 检查所有 BattleUnit，若血量<=0 则销毁并移出行动队列
            var allUnitsInScene = GameObject.FindObjectsOfType<BattleUnit>();
            if (allUnitsInScene != null && allUnitsInScene.Length >0)
            {
                foreach (var u in allUnitsInScene)
                {
                    if (u == null) continue;
                    if (u.battleHp <=0)
                    {
                        // 从队列移除并销毁
                        if (turnOrder != null) turnOrder.Remove(u);
                        u.EndBattle();
                    }
                }
                //重新排序以反映队列更新
                if (turnOrder != null) turnOrder.Sort();
            }
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
    private void UpdateBattlePointsUI()
    {
        if (battlePointsText == null) return;
        battlePointsText.text = $"当前战技点：{battlePoints}/{battlePointsMax}";
    }
}
