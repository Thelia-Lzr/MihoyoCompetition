using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.ForBattle;
using Assets.Scripts.ForBattle.Indicators;

public class LuminaController : PlayerController
{
    [Header("Lumina Settings")]
    public float powerCPlus = 1.0f; // 虚化刃 威力C+
    public float wingLancePower = 1.2f; // 羽翼之枪（光属性，魔法）
    public float starlightPower = 1.3f; // 星光射线（光属性，魔法）

    [Header("BP Costs")]
    public int costPureHeal = 4; //纯白疗愈
    public int costPhaseBlade = 2; // 虚化刃
    public int costCausalityForecast = 4; // 因果律预测
    public int costCrystalField = 5; // 水晶结界
    public int costPhantasm = 3; // 幻化
    public int costEndPhantasm = 0; //结束幻化

    [Header("Durations / Effects")]
    public int phantasmTurns = 0; // 幻化回合计数。>0 表示幻化中
    public int angelBlessingTurns = 0; // 天使赐福回合计数（降低BP，攻击上升）
    public int angelBlessingBPCostReduction = 1; // 每个技能BP减少量（>=0）

    public int teamBuffDuration = 3; // 因果律预测 / 水晶结界 持续回合
    public float buffAttackUpRate = 0.2f; // 攻击提升20%
    public float buffDefUpRate = 0.2f; // 防御提升20%
    public float healRatioOnSideHit = 0.3f; // 虚化刃：侧面命中按伤害比值回血
    public float regenPercentPerTurn = 0.05f; //结界：每回合回血百分比（最大生命）

    [Header("AOE Settings")]
    public float wingLanceRadius = 5.5f; // 羽翼之枪 扇形半径
    public float wingLanceAngle = 90f; // 羽翼之枪 扇形角度
    public float starlightLength = 10f; // 星光射线半径（改为使用扇形半径）
    public float starlightAngle = 20f; // 星光射线扇形角度（可调小以模拟直线）
    public float starlightWidth = 2f; //旧：直线判定宽度（不再使用）

    private readonly List<string> baseSkills = new List<string>
 {
 "纯白疗愈",
 "虚化刃",
 "因果律预测",
 "水晶结界",
 "幻化"
 };

    private readonly List<string> phantasmSkills = new List<string>
 {
 "星光射线",
 "羽翼之枪",
 "因果律预测",
 "天使赐福",
 "结束幻化"
 };

    protected override List<string> GetSkillNames()
    {
        return phantasmTurns > 0 ? phantasmSkills : baseSkills;
    }

    protected override string GetSkillExtraInfo(int index)
    {
        // 显示 BP 消耗与类型简述
        if (phantasmTurns > 0)
        {
            switch (index)
            {
                case 0: return $"魔法 光 威力A x{starlightPower:F2} | BP {GetCostByIndex(index)}"; // 星光射线
                case 1: return $"魔法 光 扇形 x{wingLancePower:F2} | BP {GetCostByIndex(index)}"; // 羽翼之枪
                case 2: return $"辅助 群体回避/会心+因果律 | BP {GetCostByIndex(index)}"; // 因果律预测
                case 3: return $"光环 群体攻击+降消耗 | BP {GetCostByIndex(index)}"; // 天使赐福
                case 4: return $"辅助结束幻化 | BP {GetCostByIndex(index)}"; //结束幻化
            }
        }
        else
        {
            switch (index)
            {
                case 0: return $"回复 群体 | BP {GetCostByIndex(index)}"; //纯白疗愈
                case 1: return $"魔法 威力C+ x{powerCPlus:F2} | BP {GetCostByIndex(index)}"; // 虚化刃
                case 2: return $"辅助 群体回避/会心+因果律 | BP {GetCostByIndex(index)}"; // 因果律预测
                case 3: return $"领域 群体防御+缓回 | BP {GetCostByIndex(index)}"; // 水晶结界
                case 4: return $"光环 幻化(4回合) | BP {GetCostByIndex(index)}"; // 幻化
            }
        }
        return null;
    }

    protected override float GetSkillCastRange(int index)
    {
        if (phantasmTurns > 0)
        {
            switch (index)
            {
                case 0: // 星光射线 扇形
                case 1: // 羽翼之枪 扇形
                case 3: // 天使赐福 群体
                    return targetSelectionRange;
                case 2: // 因果律预测
                    return targetSelectionRange;
                case 4: //结束幻化 自身
                    return 0f;
            }
        }
        else
        {
            switch (index)
            {
                case 0: //纯白疗愈 群体
                case 2: // 因果律预测
                case 3: // 水晶结界
                    return targetSelectionRange;
                case 1: // 虚化刃近战单体
                    return meleeRange;
                case 4: // 幻化 自身
                    return 0f;
            }
        }
        return base.GetSkillCastRange(index);
    }

    protected override void ShowSkillPreview(int index)
    {
        if (indicatorManager == null) return;

        // 清理并重建预览（同标签）
        indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.SkillPreview);
        indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.SkillRange);

        float castRange = GetSkillCastRange(index);
        Vector3 center = GetGroundPosition(transform.position);

        if (phantasmTurns > 0)
        {
            switch (index)
            {
                case 0: // 星光射线：扇形（小角度模拟直线）
                    {
                        var sector = indicatorManager.CreateSectorIndicator(transform, starlightLength, starlightAngle, BattleIndicatorManager.Tags.SkillPreview, true, "Holy");
                        //取摄像机方向
                        Camera cam = Camera.main ?? (Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null);
                        if (cam != null)
                        {
                            Vector3 camFwd = cam.transform.forward; camFwd.y = 0f;
                            if (camFwd.sqrMagnitude > 0.001f)
                            {
                                indicatorManager.UpdateSectorRotation(sector, transform, camFwd.normalized);
                            }
                        }
                        // 可选：外圈提示
                        indicatorManager.CreateCircleIndicator(center, starlightLength, true, true, BattleIndicatorManager.Tags.SkillRange, true);
                        break;
                    }
                case 1: // 羽翼之枪：扇形
                    {
                        var sector = indicatorManager.CreateSectorIndicator(transform, wingLanceRadius, wingLanceAngle, BattleIndicatorManager.Tags.SkillPreview, true, "Holy");
                        Camera cam = Camera.main ?? (Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null);
                        if (cam != null)
                        {
                            Vector3 camFwd = cam.transform.forward; camFwd.y = 0f;
                            if (camFwd.sqrMagnitude > 0.001f)
                            {
                                indicatorManager.UpdateSectorRotation(sector, transform, camFwd.normalized);
                            }
                        }
                        indicatorManager.CreateCircleIndicator(center, wingLanceRadius, true, true, BattleIndicatorManager.Tags.SkillRange, true);
                        break;
                    }
                case 2: // 因果律预测：群体
                case 3: // 天使赐福：群体
                    {
                        indicatorManager.CreateCircleIndicator(center, castRange, true, true, BattleIndicatorManager.Tags.SkillRange, true, "Buff");
                        break;
                    }
                case 4: //结束幻化
                    break;
            }
        }
        else
        {
            switch (index)
            {
                case 0: //纯白疗愈：群体
                case 2: // 因果律预测
                case 3: // 水晶结界
                    {
                        indicatorManager.CreateCircleIndicator(center, castRange, true, true, BattleIndicatorManager.Tags.SkillRange, true, "Heal");
                        break;
                    }
                case 1: // 虚化刃：单体近战
                    {
                        // 高亮最近有效目标
                        BattleUnit target = FindNearestInRange(meleeRange);
                        indicatorManager.CreateCircleIndicator(center, meleeRange, true, true, BattleIndicatorManager.Tags.SkillRange, true);
                        if (target != null) indicatorManager.CreateTargetMarker(target.transform, true);
                        break;
                    }
                case 4: // 幻化：自身
                    break;
            }
        }
    }

    private int GetCostByIndex(int index)
    {
        int baseCost = 0;
        if (phantasmTurns > 0)
        {
            switch (index)
            {
                case 0: baseCost = costPureHeal; break; // 星光射线 替换纯白疗愈（沿用costPureHeal）
                case 1: baseCost = costPhaseBlade; break; // 羽翼之枪 替换虚化刃（沿用costPhaseBlade）
                case 2: baseCost = costCausalityForecast; break; // 因果律预测
                case 3: baseCost = costCrystalField; break; // 天使赐福 替换水晶结界（沿用costCrystalField）
                case 4: baseCost = costEndPhantasm; break; //结束幻化
            }
        }
        else
        {
            switch (index)
            {
                case 0: baseCost = costPureHeal; break;
                case 1: baseCost = costPhaseBlade; break;
                case 2: baseCost = costCausalityForecast; break;
                case 3: baseCost = costCrystalField; break;
                case 4: baseCost = costPhantasm; break;
            }
        }
        if (angelBlessingTurns > 0)
        {
            baseCost = Mathf.Max(0, baseCost - angelBlessingBPCostReduction);
        }
        return baseCost;
    }

    private int CalcMagicDamage(float powerMul)
    {
        return Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * powerMul));
    }

    private bool IsSide(BattleUnit target)
    {
        Vector3 toAttacker = (transform.position - target.transform.position).normalized;
        float dot = Mathf.Abs(Vector3.Dot(target.transform.right, toAttacker));
        return dot > 0.7f;
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

    protected override IEnumerator TryQuickCastSkill(int index)
    {
        var tm = Object.FindObjectOfType<BattleTurnManager>();
        int cost = GetCostByIndex(index);
        if (tm != null && !tm.CanSpendBattlePoints(cost))
        {
            if (sfxPlayer != null) sfxPlayer.Play("Error");
            skillReselectRequested = true;
            yield break;
        }

        if (phantasmTurns > 0)
        {
            switch (index)
            {
                case 0: // 星光射线：使用扇形判定（半径+角度）
                    {
                        if (tm != null && !tm.TrySpendBattlePoints(cost)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
                        Vector3 origin = transform.position;
                        Vector3 forward = transform.forward;
                        var cam = Camera.main;
                        if (cam != null)
                        {
                            var cf = cam.transform.forward; cf.y = 0;
                            if (cf.sqrMagnitude > 0.001f) forward = cf.normalized;
                        }
                        int hitCount = 0;
                        foreach (var u in Object.FindObjectsOfType<BattleUnit>())
                        {
                            if (!IsValidTarget(u)) continue;
                            Vector3 dir = (u.transform.position - origin); dir.y = 0f;
                            float dist = dir.magnitude;
                            if (dist <= starlightLength)
                            {
                                float ang = Vector3.Angle(forward, dir.normalized);
                                if (ang <= starlightAngle * 0.5f)
                                {
                                    skillSystem?.CauseDamage(u, unit, CalcMagicDamage(starlightPower), DamageType.Magic);
                                    // 魔防下降：-15
                                    u.battleMagicDef -= 15; u.deltaMagicDef += 15; u.debuffTurns_MagicDefDown = Mathf.Max(u.debuffTurns_MagicDefDown, teamBuffDuration);
                                    //u.luminaDownMagicDef = 3;
                                    hitCount++;
                                }
                            }
                        }
                        if (hitCount >= 2)
                        {
                            foreach (var u in Object.FindObjectsOfType<BattleUnit>())
                            {
                                if (!IsValidTarget(u)) continue;
                                Vector3 dir = (u.transform.position - origin); dir.y = 0f;
                                float dist = dir.magnitude;
                                if (dist <= starlightLength)
                                {
                                    float ang = Vector3.Angle(forward, dir.normalized);
                                    if (ang <= starlightAngle * 0.5f)
                                    {
                                        u.battleMagicAtk -= 15; u.deltaMagicAtk += 15; u.debuffTurns_MagicAtkDown = Mathf.Max(u.debuffTurns_MagicAtkDown, teamBuffDuration);
                                        //u.luminaDownMagicAtk = 3;
                                    }
                                }
                            }
                        }
                        skillReselectRequested = false;
                        sfxPlayer.Play("light_magic");
                        yield return new WaitForSeconds(0.5f);
                        yield break;
                    }
                case 1: // 羽翼之枪：大范围扇形，按命中数量给自己回血
                    {
                        if (tm != null && !tm.TrySpendBattlePoints(cost)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
                        int applied = 0;
                        Vector3 origin = transform.position;
                        Vector3 forward = transform.forward; var cam = Camera.main; if (cam != null) { var cf = cam.transform.forward; cf.y = 0; if (cf.sqrMagnitude > 0.001f) forward = cf.normalized; }
                        foreach (var u in Object.FindObjectsOfType<BattleUnit>())
                        {
                            if (!IsValidTarget(u)) continue;
                            Vector3 dir = (u.transform.position - origin); dir.y = 0f;
                            if (dir.magnitude <= wingLanceRadius)
                            {
                                float ang = Vector3.Angle(forward, dir.normalized);
                                if (ang <= wingLanceAngle * 0.5f)
                                {
                                    skillSystem?.CauseDamage(u, unit, CalcMagicDamage(wingLancePower), DamageType.Magic);
                                    applied++;
                                }
                            }
                        }
                        if (applied > 0 && skillSystem != null)
                        {
                            int heal = Mathf.RoundToInt(unit.battleMagicAtk * 0.2f * applied);
                            skillSystem.Heal(unit, heal);
                        }
                        skillReselectRequested = false;
                        sfxPlayer.Play("light_magic");
                        yield return new WaitForSeconds(0.5f);
                        yield break;
                    }
                case 2: // 因果律预测：群体回避上升、会心上升、+因果律
                    {
                        if (tm != null && !tm.TrySpendBattlePoints(cost)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
                        foreach (var u in Object.FindObjectsOfType<BattleUnit>())
                        {
                            if (u.unitType != unit.unitType) continue; // 己方
                                                                       // 回避上升
                            u.luminaUpCri = 3;
                            u.luminaUpEvation = 3;
                            //int incEvd = Mathf.Max(1, Mathf.RoundToInt(u.battleSpdDef * 0.2f));
                            //u.battleSpdDef += incEvd; u.deltaSpdDef += incEvd; u.buffTurns_EvasionUp = Mathf.Max(u.buffTurns_EvasionUp, teamBuffDuration);
                            //// 会心上升
                            //int incCri = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, u.battleCri) * 0.2f));
                            //u.battleCri += incCri; u.deltaCri += incCri; u.buffTurns_CritUp = Mathf.Max(u.buffTurns_CritUp, teamBuffDuration);
                            // 因果律：必定回避一次
                            u.causality = Mathf.Max(u.causality, 1);
                        }
                        skillReselectRequested = false;
                        sfxPlayer.Play("cure");
                        yield return new WaitForSeconds(0.3f);
                        yield break;
                    }
                case 3: // 天使赐福：群体攻击上升+降低行动点消耗
                    {
                        if (tm != null && !tm.TrySpendBattlePoints(cost)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
                        foreach (var u in Object.FindObjectsOfType<BattleUnit>())
                        {
                            if (u.unitType != unit.unitType) continue;
                            int incAtk = Mathf.Max(1, Mathf.RoundToInt(u.battleAtk * buffAttackUpRate));
                            
                            u.battleAtk += incAtk; u.deltaAtk += incAtk; u.buffTurns_AttackUp = Mathf.Max(u.buffTurns_AttackUp, teamBuffDuration);
                        }
                        angelBlessingTurns = teamBuffDuration;
                        skillReselectRequested = false;
                        yield return new WaitForSeconds(0.3f);
                        yield break;
                    }
                case 4: //结束幻化
                    {
                        phantasmTurns = 0;
                        skillReselectRequested = false;
                        yield return null;
                        yield break;
                    }
            }
        }
        else
        {
            switch (index)
            {
                case 0: //纯白疗愈：群体回血
                    {
                        if (tm != null && !tm.TrySpendBattlePoints(cost)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
                        foreach (var u in Object.FindObjectsOfType<BattleUnit>())
                        {
                            if (u.unitType != unit.unitType) continue;
                            int amount = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * 1.2f));
                            skillSystem?.Heal(u, amount);
                        }
                        skillReselectRequested = false;
                        sfxPlayer.Play("cure");
                        yield return new WaitForSeconds(0.4f);
                        yield break;
                    }
                case 1: // 虚化刃：魔法C+，侧面命中为 Lumina 回复生命
                    {
                        BattleUnit target = RaycastUnitUnderCursor(Camera.main);
                        if (target == null) target = FindNearestInRange(meleeRange);
                        if (target == null || !IsValidTarget(target) || Vector3.Distance(transform.position, target.transform.position) > meleeRange)
                        {
                            if (sfxPlayer != null) sfxPlayer.Play("Error");
                            skillReselectRequested = true; yield break;
                        }
                        if (tm != null && !tm.TrySpendBattlePoints(cost)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
                        int dmg = CalcMagicDamage(powerCPlus);
                        skillSystem?.CauseDamage(target, unit, dmg, DamageType.Magic);
                        if (IsSide(target) && skillSystem != null)
                        {
                            int heal = Mathf.Max(1, Mathf.RoundToInt(dmg * healRatioOnSideHit));
                            skillSystem.Heal(unit, heal);
                        }
                        skillReselectRequested = false;
                        sfxPlayer.Play("cut");
                        yield return new WaitForSeconds(0.3f);
                        yield break;
                    }
                case 2: // 因果律预测：群体回避/会心+因果律
                    {
                        if (tm != null && !tm.TrySpendBattlePoints(cost)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
                        foreach (var u in Object.FindObjectsOfType<BattleUnit>())
                        {
                            if (u.unitType != unit.unitType) continue;
                            u.luminaUpCri = 3;
                            u.luminaUpEvation = 3;
                            //int incEvd = Mathf.Max(1, Mathf.RoundToInt(u.battleSpdDef * 0.2f));
                            //u.battleSpdDef += incEvd; u.deltaSpdDef += incEvd; u.buffTurns_EvasionUp = Mathf.Max(u.buffTurns_EvasionUp, teamBuffDuration);
                            //int incCri = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, u.battleCri) * 0.2f));
                            //u.battleCri += incCri; u.deltaCri += incCri; u.buffTurns_CritUp = Mathf.Max(u.buffTurns_CritUp, teamBuffDuration);
                            u.causality = Mathf.Max(u.causality, 1);
                        }
                        skillReselectRequested = false;
                        sfxPlayer.Play("light_magic");
                        yield return new WaitForSeconds(0.3f);
                        yield break;
                    }
                case 3: // 水晶结界：群体防御上升+缓慢回血
                    {
                        if (tm != null && !tm.TrySpendBattlePoints(cost)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
                        foreach (var u in Object.FindObjectsOfType<BattleUnit>())
                        {
                            if (u.unitType != unit.unitType) continue;
                            int incDef = Mathf.Max(1, Mathf.RoundToInt(u.battleDef * buffDefUpRate));
                            u.battleDef += incDef; u.deltaDef += incDef; u.buffTurns_DefUp = Mathf.Max(u.buffTurns_DefUp, teamBuffDuration);
                            // regen 持续
                            u.buffTurns_Regen = Mathf.Max(u.buffTurns_Regen, teamBuffDuration);
                            u.regenPerTurn = Mathf.Max(u.regenPerTurn, Mathf.Max(1, Mathf.RoundToInt(u.battleMaxHp * regenPercentPerTurn)));
                        }
                        skillReselectRequested = false;
                        sfxPlayer.Play("light_magic");
                        yield return new WaitForSeconds(0.3f);
                        yield break;
                    }
                case 4: // 幻化：赋值4回合
                    {
                        if (tm != null && !tm.TrySpendBattlePoints(cost)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
                        sfxPlayer.Play("light_magic");
                        phantasmTurns = 4;
                        skillReselectRequested = false;
                        yield return null;
                        yield break;
                    }
            }
        }

        // 未匹配：认为失败
        if (sfxPlayer != null) sfxPlayer.Play("Error");
        skillReselectRequested = true;
        yield break;
    }

    // 回合开始时衰减幻化和赐福
    public override IEnumerator ExecuteTurn(BattleTurnManager turnManager)
    {
        // 在进入基类逻辑前，先衰减自身独有的计数（因 SkillSystem 不处理它们）
        if (phantasmTurns > 0) phantasmTurns = Mathf.Max(0, phantasmTurns - 1);
        if (angelBlessingTurns > 0) angelBlessingTurns = Mathf.Max(0, angelBlessingTurns - 1);
        yield return base.ExecuteTurn(turnManager);
    }
}
