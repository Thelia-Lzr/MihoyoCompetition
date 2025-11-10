using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.ForBattle.UI;
using Assets.Scripts.ForBattle;
using Assets.Scripts.ForBattle.Indicators;

public class FaytController : PlayerController
{
    [Header("Fayt Skills Settings")]
    // Power baselines (as % of battleAtk)
    public float powerC = 0.8f; // 撕裂连击（每段）
    public float powerB = 1.2f; // 背后斩击
    public float powerBPlus = 1.4f; // 流水横扫
    public float powerAPlus = 1.8f; //轮转强袭

    [Header("Skill Costs (BP)")]
    public int costBackstab = 3; // 背后斩击
    public int costRendCombo = 2; // 撕裂连击
    public int costShadowEye = 3; // 暗影之眼
    public int costWaterSweep = 5; // 流水横扫
    public int costRotaryAssault = 6;//轮转强袭
    public int costLateAdvantage = 0;// 后发优势

    [Header("AoE Settings")]
    public float waterSweepRadius = 4.5f; // 流水强袭扇形半径
    public float waterSweepAngle = 90f; // 流水强袭扇形角度

    private readonly List<string> faytSkills = new List<string>
    {
        "背后斩击",
        "撕裂连击",
        "暗影之眼",
        "流水横扫",
        "轮转强袭",
        "后发优势"
    };

    protected override List<string> GetSkillNames()
    {
        return faytSkills;
    }

    protected override string GetSkillExtraInfo(int index)
    {
        // 显示威力评级 + 战技点消耗
        switch (index)
        {
            case 0: return $"威力B x{powerB:F2} | {FormatBpCost(costBackstab)}";
            case 1: return $"威力C x{powerC:F2} | {FormatBpCost(costRendCombo)}";
            case 2: return $"辅助 | {FormatBpCost(costShadowEye)}";
            case 3: return $"威力B+ x{powerBPlus:F2} | {FormatBpCost(costWaterSweep)}";
            case 4: return $"威力A+ x{powerAPlus:F2} | {FormatBpCost(costRotaryAssault)}";
            case 5: return $"辅助 | {FormatBpCost(costLateAdvantage)}";
        }
        return null;
    }

    protected override float GetSkillCastRange(int index)
    {
        switch (index)
        {
            case 0: // 背后斩击
            case 1: // 撕裂连击
            case 4: //轮转强袭
                return meleeRange;
            case 3: // 流水横扫（使用方向扇形，以 targetSelectionRange作为前沿半径上限显示）
                return targetSelectionRange;
            case 2: // 暗影之眼 自身
            case 5: // 后发优势 自身
                return 0f;
        }
        return base.GetSkillCastRange(index);
    }

    // 标记单体技能，使基类的单体目标循环与指示器逻辑生效
    protected override bool IsSkillSingleTarget(int index)
    {
        return index == 0 || index == 1 || index == 4; // 三个近战单体技能
    }

    protected override void ShowSkillPreview(int index)
    {
        if (indicatorManager == null) return;
        //indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.SkillPreview);

        var tm = FindObjectOfType<BattleTurnManager>();
        bool enough = true;
        if (tm != null)
        {
            int c = GetCostByIndex(index);
            enough = tm.CanSpendBattlePoints(c);
        }

        if (index == 0 || index == 1 || index == 4)
        {
            // 近战单体技能显示自身的近战范围圈；目标标记交给基类逻辑处理
            float castRange = GetSkillCastRange(index);
            if (indicatorManager != null)
            {
                Vector3 center = GetGroundPosition(transform.position);
                if (skillRangeIndicator == null)
                {
                    skillRangeIndicator = indicatorManager.CreateCircleIndicator(
                    center,
                    castRange,
                    true,
                    true,
                    BattleIndicatorManager.Tags.SkillRange,
                    true
                    );
                }
                else
                {
                    indicatorManager.UpdateCircleIndicator(skillRangeIndicator, center, castRange, true);
                }
            }
        }
        else if (index == 3)
        {
            float castRange = GetSkillCastRange(index);
            var center = GetGroundPosition(transform.position);
            // 扇形范围：以玩家为圆心，朝向根据镜头方向实时变化
            var sector = indicatorManager.CreateSectorIndicator(
                transform,
                waterSweepRadius,
                waterSweepAngle,
                BattleIndicatorManager.Tags.SkillPreview,
                true,
                "Water");
            

            //取摄像机前向并投影到水平面作为朝向
            Camera cam = Camera.main ?? (Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null);
            if (cam != null)
            {
                Vector3 camFwd = cam.transform.forward; camFwd.y = 0f;
                if (camFwd.sqrMagnitude > 0.001f)
                {
                    indicatorManager.UpdateSectorRotation(sector, transform, camFwd.normalized);
                }
            }
            indicatorManager.UpdateCircleIndicator(skillRangeIndicator, center, castRange, true);
        }
        
    }

    private int GetCostByIndex(int index)
    {
        switch (index)
        {
            case 0: return costBackstab;
            case 1: return costRendCombo;
            case 2: return costShadowEye;
            case 3: return costWaterSweep;
            case 4: return costRotaryAssault;
            case 5: return costLateAdvantage;
        }
        return 0;
    }

    private int CalcDamage(float powerMul)
    {
        // 基于当前攻击力的伤害
        return Mathf.Max(1, Mathf.RoundToInt(unit.battleAtk * powerMul));
    }

    private bool IsBehind(BattleUnit target)
    {
        Vector3 toAttacker = (transform.position - target.transform.position).normalized;
        // dot >0 → 攻击者在目标前方；dot <0 → 攻击者在目标后方
        float dot = Vector3.Dot(target.transform.forward, toAttacker);
        // < -0.5约等于在背后60°以内
        return dot < -0.5f;
    }

    private bool IsSide(BattleUnit target)
    {
        Vector3 toAttacker = (transform.position - target.transform.position).normalized;
        float dot = Mathf.Abs(Vector3.Dot(target.transform.right, toAttacker));
        // 与左右方向接近，认为侧面
        return dot > 0.7f;
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

    // 快速释放：左键直接尝试施放
    protected override IEnumerator TryQuickCastSkill(int index)
    {
        var tm = FindObjectOfType<BattleTurnManager>();
        int baseCost = GetCostByIndex(index);
        int effCost = GetEffectiveBpCost(baseCost);
        if (tm != null && !tm.CanSpendBattlePoints(baseCost))
        {
            if (tm != null && !tm.CanSpendBattlePoints(effCost)) { sfxPlayer?.Play("Error"); skillReselectRequested = true; yield break; }
        }

        Camera cam = Camera.main;
        switch (index)
        {
            case 0: // 背后斩击：单体近战，背后伤害翻倍
                {
                    BattleUnit target = RaycastUnitUnderCursor(cam);
                    if (target == null) target = FindNearestInRange(meleeRange);
                    if (target == null || !IsValidTarget(target) || Vector3.Distance(transform.position, target.transform.position) > meleeRange)
                    {
                        if (sfxPlayer != null) sfxPlayer.Play("Error");
                        skillReselectRequested = true;
                        yield break;
                    }
                    if (!tm.TrySpendBattlePoints(baseCost)) { sfxPlayer?.Play("Error"); skillReselectRequested = true; yield break; }
                    int dmg = CalcDamage(powerB);
                    if (IsBehind(target)) dmg *= 2;
                    skillSystem?.CauseDamage(target, unit, dmg, DamageType.Physics);
                    sfxPlayer?.Play("cut"); skillReselectRequested = false; yield return new WaitForSeconds(0.4f); yield break;
                }
            case 1: // 撕裂连击：单体近战，两段，侧面额外提升（以伤害加作为例）
                {
                    BattleUnit target = RaycastUnitUnderCursor(cam);
                    if (target == null) target = FindNearestInRange(meleeRange);
                    if (target == null || !IsValidTarget(target) || Vector3.Distance(transform.position, target.transform.position) > meleeRange)
                    {
                        if (sfxPlayer != null) sfxPlayer.Play("Error");
                        skillReselectRequested = true;
                        yield break;
                    }
                    if (!tm.TrySpendBattlePoints(baseCost)) { sfxPlayer?.Play("Error"); skillReselectRequested = true; yield break; }
                    float bonus = IsSide(target) ? 1.25f : 1f;
                    int hit1 = Mathf.RoundToInt(CalcDamage(powerC) * bonus);
                    int hit2 = Mathf.RoundToInt(CalcDamage(powerC) * bonus);
                    skillSystem?.CauseDamage(target, unit, hit1, DamageType.Physics);
                    sfxPlayer?.Play("cut"); yield return new WaitForSeconds(0.4f);
                    skillSystem?.CauseDamage(target, unit, hit2, DamageType.Physics);
                    sfxPlayer?.Play("cut"); skillReselectRequested = false; yield break;
                }
            case 2: // 暗影之眼：自身增益与隐身（这里以日志提示为主）
                {
                    if (!tm.TrySpendBattlePoints(baseCost)) { sfxPlayer?.Play("Error"); skillReselectRequested = true; yield break; }
                    Debug.Log("[Fayt] 暗影之眼：攻击上升并获得隐身（占位实现）");
                    //unit.battleAtk = Mathf.RoundToInt(unit.battleAtk * 1.2f); // 提高20%攻击力（占位实现）
                    unit.faytUpAtk = 3;
                    unit.invisible = 1;
                    // TODO: 接入状态系统：提高攻击、设置隐身标记
                    skillReselectRequested = false;
                    yield break;
                }
            case 3: // 流水横扫：扇形范围群体伤害并降防
                {
                    if (!tm.TrySpendBattlePoints(baseCost)) { sfxPlayer?.Play("Error"); skillReselectRequested = true; yield break; }
                    int applied = 0;
                    Vector3 origin = transform.position;
                    // 使用镜头方向作为朝向（使用外层 cam变量）
                    Vector3 forward = transform.forward;
                    if (cam != null)
                    {
                        Vector3 camFwd = cam.transform.forward; camFwd.y = 0f;
                        if (camFwd.sqrMagnitude > 0.001f) forward = camFwd.normalized;
                    }
                    foreach (var u in FindObjectsOfType<BattleUnit>())
                    {
                        if (!IsValidTarget(u)) continue;
                        Vector3 dir = (u.transform.position - origin); dir.y = 0f;
                        if (dir.magnitude <= waterSweepRadius)
                        {
                            float ang = Vector3.Angle(forward, dir.normalized);
                            if (ang <= waterSweepAngle * 0.5f)
                            {
                                skillSystem?.CauseDamage(u, unit, CalcDamage(powerBPlus), DamageType.Physics);
                                // 简易降防（占位）：降低10 点物防
                                //u.battleDef = Mathf.Max(0, u.battleDef - 10);
                                u.faytDownDef = 3;
                                applied++;
                            }
                        }
                    }
                    Debug.Log($"[Fayt] 流水横扫 命中 {applied} 个目标");
                    skillReselectRequested = false;
                    yield break;
                }
            case 4: //轮转强袭：单体近战大伤害
                {
                    BattleUnit target = RaycastUnitUnderCursor(cam);
                    if (target == null) target = FindNearestInRange(meleeRange);
                    if (target == null || !IsValidTarget(target) || Vector3.Distance(transform.position, target.transform.position) > meleeRange)
                    {
                        if (sfxPlayer != null) sfxPlayer.Play("Error");
                        skillReselectRequested = true;
                        yield break;
                    }
                    if (!tm.TrySpendBattlePoints(baseCost)) { sfxPlayer?.Play("Error"); skillReselectRequested = true; yield break; }
                    skillSystem?.CauseDamage(target, unit, CalcDamage(powerAPlus), DamageType.Physics);
                    skillReselectRequested = false;
                    yield break;
                }
            case 5: // 后发优势：损失一半生命，回复4BP
                {
                    if (!tm.TrySpendBattlePoints(baseCost)) { sfxPlayer?.Play("Error"); skillReselectRequested = true; yield break; }
                    int half = Mathf.Max(1, unit.battleHp / 2);
                    skillSystem?.CauseDamage(unit, unit, half, DamageType.True);
                    //unit.battleHp = Mathf.Max(1, unit.battleHp - half);
                    tm.AddBattlePoints(4);
                    skillReselectRequested = false;
                    yield break;
                }
        }

        sfxPlayer?.Play("Error"); skillReselectRequested = true; yield break;
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
}
