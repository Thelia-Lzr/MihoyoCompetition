using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.ForBattle;
using Assets.Scripts.ForBattle.Indicators;

public class BlakeController : PlayerController
{
    [Header("Blake Power (multiplier on battleAtk)")]
    public float powerC = 0.9f;     // 碎骨上挑 C（低伤、高破盾/打断）
    public float powerB = 1.2f;     // 战锤强击 B
    public float powerBPlus = 1.4f; // 回旋战斧 B+
    public float powerA = 1.8f;     // 鲜血终结 A

    [Header("Blake Costs (BP)")]
    public int costDefenseInstinct = 3; // 防守直觉（光环）
    public int costBerserker = 4;       // 狂战士（光环）
    public int costHammerStrike = 3;    // 战锤强击（单体近战）
    public int costBoneUppercut = 2;    // 碎骨上挑（单体近战，打断/上挑）
    public int costWhirlAxe = 5;        // 回旋战斧（自身为中心圆 AoE）
    public int costBloodFinale = 6;     // 鲜血终结（大范围直线，自损30%）

    [Header("AoE Settings")]
    public float whirlAxeRadius = 4.0f; // 回旋战斧半径
    public int whirlAxeMinTargets = 2;  // 返还 AP 的最小命中目标数
    public float bloodFinaleLength = 10f; // 鲜血终结长度
    public float bloodFinaleWidth = 3.0f;  // 鲜血终结宽度（直线宽）

    private readonly List<string> blakeSkills = new List<string>
    {
        "防守直觉",
        "狂战士",
        "战锤强击",
        "碎骨上挑",
        "回旋战斧",
        "鲜血终结"
    };

    protected override List<string> GetSkillNames() => blakeSkills;

    protected override string GetSkillExtraInfo(int index)
    {
        switch (index)
        {
            case 0: return $"光环 | {FormatBpCost(costDefenseInstinct)}";
            case 1: return $"光环 | {FormatBpCost(costBerserker)}";
            case 2: return $"物理 B x{powerB:F2} | {FormatBpCost(costHammerStrike)}";
            case 3: return $"物理 C x{powerC:F2} | {FormatBpCost(costBoneUppercut)}";
            case 4: return $"物理 B+ x{powerBPlus:F2} | {FormatBpCost(costWhirlAxe)}";
            case 5: return $"物理 A x{powerA:F2} | {FormatBpCost(costBloodFinale)}";
        }
        return null;
    }

    protected override float GetSkillCastRange(int index)
    {
        switch (index)
        {
            case 2: // 战锤强击
            case 3: // 碎骨上挑
                return meleeRange;
            case 4: // 回旋战斧（自身为中心）
                return whirlAxeRadius;
            case 5: // 鲜血终结（直线，长度用选择范围）
                return targetSelectionRange;
            default:
                return 0f; // 光环类为自身
        }
    }

    protected override bool IsSkillSingleTarget(int index)
    {
        return index == 2 || index == 3; // 两个单体近战
    }

    protected override void ShowSkillPreview(int index)
    {
        if (indicatorManager == null) return;
        var center = GetGroundPosition(transform.position);

        if (index == 2 || index == 3)
        {
            // 单体近战：显示近战范围圈
            indicatorManager.CreateCircleIndicator(center, meleeRange, true, true, BattleIndicatorManager.Tags.SkillRange, true);
        }
        else if (index == 4)
        {
            // 自身为中心圆
            indicatorManager.CreateCircleIndicator(center, whirlAxeRadius, true, false, BattleIndicatorManager.Tags.SkillPreview, true, "Blood");
            indicatorManager.CreateCircleIndicator(center, whirlAxeRadius, true, true, BattleIndicatorManager.Tags.SkillRange, true);
        }
        else if (index == 5)
        {
            // 直线：使用矩形指示（取摄像机朝向投影到水平面）
            Vector3 forward = transform.forward;
            var cam = Camera.main; if (cam != null)
            {
                Vector3 f = cam.transform.forward; f.y = 0f; if (f.sqrMagnitude > 0.001f) forward = f.normalized;
            }
            indicatorManager.CreateRectangleIndicator(transform, bloodFinaleLength, bloodFinaleWidth, forward, BattleIndicatorManager.Tags.SkillPreview, true, "Heavy");
        }
    }

    private int CalcDamage(float mul) => Mathf.Max(1, Mathf.RoundToInt(unit.battleAtk * mul));

    protected override IEnumerator TryQuickCastSkill(int index)
    {
        var tm = FindObjectOfType<BattleTurnManager>();
        int baseCost = GetCost(index);
        int effCost = GetEffectiveBpCost(baseCost);
        if (tm != null && !tm.CanSpendBattlePoints(baseCost)) // CanSpendBattlePoints already applies barrier reduction via manager; fallback using effCost
        {
            if (tm != null && !tm.CanSpendBattlePoints(effCost))
            {
                sfxPlayer?.Play("Error");
                skillReselectRequested = true;
                yield break;
            }
        }
        // Spend using TrySpendBattlePoints which already accounts for barrier (through BattleTurnManager.GetAdjustedCost)
        // In case manager isn't barrier-aware, pass base cost and let manager adjust. This ensures central logic remains single source of truth.
        switch (index)
        {
            case 0: // 防守直觉：+DEF -ATK，3回合
            {
                if (!tm.TrySpendBattlePoints(baseCost)) { sfxPlayer?.Play("Error"); skillReselectRequested = true; yield break; }
                unit.blakeDefenseAura = Mathf.Max(unit.blakeDefenseAura, 3);
                if (sfxPlayer != null) sfxPlayer.Play("cut");
                skillReselectRequested = false;
                yield break;
            }
            case 1: // 狂战士：+ATK -DEF，3回合
            {
                if (!tm.TrySpendBattlePoints(baseCost)) { sfxPlayer?.Play("Error"); skillReselectRequested = true; yield break; }
                unit.blakeBerserkAura = Mathf.Max(unit.blakeBerserkAura, 3);
                if (sfxPlayer != null) sfxPlayer.Play("cut");
                skillReselectRequested = false;
                yield break;
            }
            case 2: // 战锤强击：单体近战B；若自身处于任一光环，赋予目标嘲讽1回合
            {
                BattleUnit target = RaycastUnitUnderCursor(Camera.main) ?? FindNearestInRange(meleeRange);
                if (target == null || !IsValidTarget(target) || Vector3.Distance(transform.position, target.transform.position) > meleeRange)
                { sfxPlayer?.Play("Error"); skillReselectRequested = true; yield break; }
                if (!tm.TrySpendBattlePoints(baseCost)) { sfxPlayer?.Play("Error"); skillReselectRequested = true; yield break; }
                skillSystem?.CauseDamage(target, unit, CalcDamage(powerB), DamageType.Physics);
                if (unit.blakeDefenseAura > 0 || unit.blakeBerserkAura > 0)
                {
                    target.blakeTaunt = Mathf.Max(target.blakeTaunt, 1);
                }
                sfxPlayer?.Play("cut");
                skillReselectRequested = false; yield return new WaitForSeconds(0.3f); yield break;
            }
            case 3: // 碎骨上挑：单体近战C；打断（占位：眩晕1回合）
            {
                BattleUnit target = RaycastUnitUnderCursor(Camera.main) ?? FindNearestInRange(meleeRange);
                if (target == null || !IsValidTarget(target) || Vector3.Distance(transform.position, target.transform.position) > meleeRange)
                { sfxPlayer?.Play("Error"); skillReselectRequested = true; yield break; }
                if (!tm.TrySpendBattlePoints(baseCost)) { sfxPlayer?.Play("Error"); skillReselectRequested = true; yield break; }
                skillSystem?.CauseDamage(target, unit, CalcDamage(powerC), DamageType.Physics);
                target.blakeStun = Mathf.Max(target.blakeStun, 1);
                sfxPlayer?.Play("cut");
                skillReselectRequested = false; yield return new WaitForSeconds(0.2f); yield break;
            }
            case 4: // 回旋战斧：自身为中心圆形B+；命中>=2时返还行动点
            {
                if (!tm.TrySpendBattlePoints(baseCost)) { sfxPlayer?.Play("Error"); skillReselectRequested = true; yield break; }
                int hitCount = 0;
                Vector3 origin = transform.position;
                foreach (var u in FindObjectsOfType<BattleUnit>())
                {
                    if (!IsValidTarget(u)) continue;
                    if (Vector3.Distance(origin, u.transform.position) <= whirlAxeRadius)
                    {
                        skillSystem?.CauseDamage(u, unit, CalcDamage(powerBPlus), DamageType.Physics);
                        hitCount++;
                    }
                }
                if (hitCount >= whirlAxeMinTargets)
                {
                    int refund = Mathf.Max(0, hitCount - whirlAxeMinTargets);
                    unit.battleActPoint += refund;
                }
                sfxPlayer?.Play("cut");
                skillReselectRequested = false; yield return new WaitForSeconds(0.3f); yield break;
            }
            case 5: // 鲜血终结：直线A，自损30%最大生命
            {
                if (!tm.TrySpendBattlePoints(baseCost)) { sfxPlayer?.Play("Error"); skillReselectRequested = true; yield break; }
                Vector3 origin = transform.position;
                Vector3 forward = transform.forward;
                var cam = Camera.main; if (cam != null) { Vector3 f = cam.transform.forward; f.y = 0f; if (f.sqrMagnitude > 0.001f) forward = f.normalized; }
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                foreach (var u in FindObjectsOfType<BattleUnit>())
                {
                    if (!IsValidTarget(u)) continue;
                    Vector3 to = u.transform.position - origin; to.y = 0f;
                    float fwd = Vector3.Dot(to, forward);
                    float lat = Mathf.Abs(Vector3.Dot(to, right));
                    if (fwd >= 0f && fwd <= bloodFinaleLength && lat <= bloodFinaleWidth * 0.5f)
                    {
                        skillSystem?.CauseDamage(u, unit, CalcDamage(powerA), DamageType.Physics);
                    }
                }
                // 自损
                int selfDmg = Mathf.Max(1, Mathf.RoundToInt(unit.battleMaxHp * 0.3f));
                skillSystem?.CauseDamage(unit, unit, selfDmg, DamageType.True);
                sfxPlayer?.Play("cut");
                skillReselectRequested = false; yield return new WaitForSeconds(0.4f); yield break;
            }
        }

        // 未匹配到：视为失败
        sfxPlayer?.Play("Error");
        skillReselectRequested = true;
        yield break;
    }

    private int GetCost(int index)
    {
        switch (index)
        {
            case 0: return costDefenseInstinct;
            case 1: return costBerserker;
            case 2: return costHammerStrike;
            case 3: return costBoneUppercut;
            case 4: return costWhirlAxe;
            case 5: return costBloodFinale;
        }
        return 0;
    }

    private BattleUnit RaycastUnitUnderCursor(Camera cam)
    {
        if (cam == null) return null;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            return hit.collider.GetComponentInParent<BattleUnit>();
        }
        return null;
    }

    private BattleUnit FindNearestInRange(float range)
    {
        BattleUnit nearest = null; float best = float.MaxValue;
        foreach (var u in FindObjectsOfType<BattleUnit>())
        {
            if (!IsValidTarget(u)) continue;
            float d = Vector3.Distance(transform.position, u.transform.position);
            if (d <= range && d < best) { best = d; nearest = u; }
        }
        return nearest;
    }

    public override void OnBattleStart()
    {
        base.OnBattleStart();
        // 重新映射动画参数，确保行走/跑步动画未丢失（某些模型导入后参数名可能不同）
        if (visualRoot != null)
        {
            var anim = visualRoot.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                cachedAnimator = anim; // 复用基类字段
                MapAnimatorParameters();
            }
        }
    }
}