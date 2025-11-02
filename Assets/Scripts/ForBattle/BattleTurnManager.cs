using Assets.Scripts.ForBattle;
using Assets.Scripts.ForBattle.Indicators;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

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
    public int battlePoints = 0;
    [Tooltip("战技点上限")]
    public int battlePointsMax = 10;
    [Tooltip("普通攻击每次获得的战技点")]
    public int pointsPerNormalAttack = 1;

    [Header("UI")]
    [Tooltip("用于显示战技点的 TextMeshPro 文本对象 (可选)")]
    public TMP_Text battlePointsText;

    [Header("Turn Settings")]
    public int actionPointThreshold = 100;
    public float tickInterval = 0.1f; // seconds per AP accumulation tick

    private Coroutine turnCoroutine = null;

    // track previous unit to enable smooth transition
    private BattleUnit previousUnit = null;

    public void StartBattle()
    {
        this.status = BattleStatus.Battle;
        battlePoints = 0;
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
    }

    /// <summary>
    /// Fast-forward AP by computing the minimum number of ticks needed for any unit to reach the threshold
    /// and adding per-tick increments in bulk to all units. Returns the number of ticks applied.
    /// </summary>
    private int FastForwardTicksToNextAction()
    {
        if (turnOrder == null || turnOrder.Count == 0) return 0;

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

    IEnumerator ProcessTurn()
    {
        // Main loop runs while status == Battle
        while (status == BattleStatus.Battle)
        {
            if (turnOrder == null || turnOrder.Count == 0)
            {
                // nothing to do
                yield return null;
                continue;
            }

            // Fast-forward AP to the next unit's turn to avoid many small ticks
            int appliedTicks = FastForwardTicksToNextAction();
            if (appliedTicks == 0)
            {
                // Fallback to small wait if no ticks applied (safety)
                yield return new WaitForSeconds(tickInterval);
            }

            // It's someone's turn
            BattleUnit currentUnit = turnOrder.Peek();
            if (currentUnit == null)
            {
                yield return null;
                continue;
            }

            if (currentUnit.battleActPoint < actionPointThreshold)
            {
                // Safety: if not reached yet, wait a small interval then loop
                yield return new WaitForSeconds(tickInterval);
                continue;
            }

            Debug.Log("当前行动单位: " + currentUnit.unitName);

            // Show movement range indicator on ground if indicatorManager available
            if (indicatorManager != null)
            {
                // try to get moveRange from controller if available (PlayerController or similar)
                float moveRange = 0f;
                var pc = currentUnit.controller as PlayerController;
                if (pc != null)
                {
                    moveRange = pc.moveRange;
                }
                else
                {
                    // fallback: try to find a MovementRange component or default value
                    // default to 3 units if unknown
                    moveRange = 3f;
                }

                // Raycast down to find the ground height and place the circle on the floor
                Vector3 origin = currentUnit.transform.position + Vector3.up * 0.5f;
                RaycastHit hit;
                Vector3 circleCenter;
                if (Physics.Raycast(origin, Vector3.down, out hit, 5f))
                {
                    circleCenter = hit.point + Vector3.up * 0.01f; // slight offset above ground
                }
                else
                {
                    // fallback to y=0 plane
                    circleCenter = new Vector3(currentUnit.transform.position.x, 0f + 0.01f, currentUnit.transform.position.z);
                }

                Debug.Log($"BattleTurnManager: Showing movement circle at {circleCenter} radius={moveRange}");
                // Use hollow circle with MovementRange tag to ensure only one exists
                indicatorManager.CreateCircleIndicator(
                    circleCenter,
                    moveRange,
                    true,
                    true,
                    BattleIndicatorManager.Tags.MovementRange,
                    true
                );
            }
            else
            {
                Debug.LogWarning("BattleTurnManager: indicatorManager is null; cannot show movement circle.");
            }

            // Focus camera and allow player to inspect; run controller or fallback to space+action
            if (cameraController != null)
            {
                Vector3 desiredOffset = new Vector3(0f, 1.2f, -3f);

                // If we have a previous unit, transition smoothly from previous to current
                if (previousUnit != null && previousUnit != currentUnit)
                {
                    // perform transition and wait
                    Transform fromT = previousUnit.cameraRoot != null ? previousUnit.cameraRoot : previousUnit.transform;
                    Transform toT = currentUnit.cameraRoot != null ? currentUnit.cameraRoot : currentUnit.transform;
                    yield return StartCoroutine(cameraController.TransitionToTarget(fromT, toT, desiredOffset, cameraController.blendTime));
                }
                else
                {
                    // No previous unit, just focus immediately
                    Transform toT = currentUnit.cameraRoot != null ? currentUnit.cameraRoot : currentUnit.transform;
                    cameraController.FocusImmediate(toT, desiredOffset);

                    // wait blend
                    yield return new WaitForSeconds(cameraController.blendTime);
                }

                // Enable mouse control while controller runs or while waiting for fallback input
                cameraController.EnableMouseControl(true);

                if (currentUnit.controller != null)
                {
                    // call controller to execute the turn
                    yield return StartCoroutine(currentUnit.controller.ExecuteTurn(this));
                }
                else
                {
                    // fallback: wait for space to confirm, then run simple action cam logic
                    Debug.Log("按空格继续到下一个单位");
                    yield return StartCoroutine(WaitForSpaceKey());

                    // choose a target (example: first other unit)
                    BattleUnit target = null;
                    var others = turnOrder.GetAll();
                    if (others != null && others.Count > 1)
                    {
                        // pick first unit that is not currentUnit
                        foreach (var u in others)
                        {
                            if (u != null && u != currentUnit)
                            {
                                target = u;
                                break;
                            }
                        }
                    }

                    if (target != null && cameraController != null)
                    {
                        Transform actorT = currentUnit.cameraRoot != null ? currentUnit.cameraRoot : currentUnit.transform;
                        Transform targetT = target.cameraRoot != null ? target.cameraRoot : target.transform;
                        yield return StartCoroutine(cameraController.PlayActionCam(actorT, targetT, new Vector3(0f,1.0f, -1.5f),0.7f));
                    }
                    else
                    {
                        // do a small wait to simulate action
                        yield return new WaitForSeconds(0.3f);
                    }
                }

                cameraController.EnableMouseControl(false);
            }
            else
            {
                // No camera controller: if unit has controller, run it, else fallback simple wait
                if (currentUnit.controller != null)
                {
                    yield return StartCoroutine(currentUnit.controller.ExecuteTurn(this));
                }
                else
                {
                    yield return new WaitForSeconds(0.3f);
                }
            }

            // finish action: subtract threshold (or reset)
            currentUnit.battleActPoint = Mathf.Max(0, currentUnit.battleActPoint - actionPointThreshold);

            // Clear movement indicator after action
            if (indicatorManager != null)
            {
                indicatorManager.ClearIndicators();
            }

            // resort after modification
            turnOrder.Sort();

            // set previous unit for next transition
            previousUnit = currentUnit;

            yield return null;
        }

        // Cleanup when exiting battle
        if (cameraController != null) cameraController.ShowOverview();
        turnCoroutine = null;
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
    public void ResetBattlePoints()
    {
        battlePoints = 0;
        UpdateBattlePointsUI();
    }

    public int GetBattlePoints()
    {
        return battlePoints;
    }

    public void AddBattlePoints(int amount)
    {
        if (amount <= 0) return;
        int before = battlePoints;
        battlePoints = Mathf.Clamp(battlePoints + amount, 0, battlePointsMax);
        Debug.Log($"[BattleTurnManager] 战技点 +{amount}: {before} -> {battlePoints}/{battlePointsMax}");
        UpdateBattlePointsUI();
    }

    public bool CanSpendBattlePoints(int amount)
    {
        return amount >= 0 && battlePoints >= amount;
    }

    public bool TrySpendBattlePoints(int amount)
    {
        if (!CanSpendBattlePoints(amount)) return false;
        battlePoints -= amount;
        Debug.Log($"[BattleTurnManager] 战技点 消耗 {amount}，剩余 {battlePoints}/{battlePointsMax}");
        UpdateBattlePointsUI();
        return true;
    }

    private void UpdateBattlePointsUI()
    {
        if (battlePointsText == null) return;
        battlePointsText.text = $"当前战技点：{battlePoints}/{battlePointsMax}";
    }
}
