using Assets.Scripts.ForBattle;
using Assets.Scripts.ForBattle.Indicators;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LuminaController : PlayerController
{
    [Header("Lumina Settings")]
    public float healGroupPower = 0.8f;            // 纯白疗愈：按魔攻倍率回血
    public float bladePowerCPlus = 1.3f;           // 虚化刃：C+ 约1.3
    public float bladeSideHealRatio = 0.2f;        // 虚化刃侧面命中，自身按伤害比例回血
    public int buffTurnsDefault = 3;               // 常规Buff持续回合
    public int auraTurnsDefault = 4;               // 幻化持续回合
    public int evasionUpAmount = 20;               // 因果律预测：回避上升
    public int critUpAmount = 20;                  // 因果律预测：会心上升
    public int defenseUpAmount = 20;               // 水晶结界：防御上升
    public float regenRatioPerTurn = 0.05f;        // 水晶结界：每回合按最大生命比例回血
    public int attackUpAmount = 20;                // 天使赐福：攻击上升
    public int speedUpAmount = 15;                 // 天使赐福：行动效率（用速度上升模拟）
    public int magicDefDownAmount = 20;            // 星光射线：魔防下降
    public int magicAtkDownAmount = 15;            // 星光射线：魔攻下降（命中>=2时）
    public float starlightLength = 8f;             // 星光射线：长度
    public float starlightWidth = 1.25f;           // 星光射线：半宽
    public float spearRadius = 6.0f;               // 羽翼之枪：扇形半径
    public float spearAngle = 100f;                // 羽翼之枪：扇形角度
    public float spearPower = 1.2f;                // 羽翼之枪：伤害倍率（魔法）
    public float spearSelfHealPerTargetRatio = 0.05f; // 羽翼之枪：每命中1目标，自身按最大生命回血比例

    private bool isTransformed = false;
    private int transformTurnsLeft = 0;

    protected override List<string> GetSkillNames()
    {
        if (!isTransformed)
        {
            return new List<string> {
                "纯白疗愈",   // 0  4BP 群体回血
                "虚化刃",     // 1  2BP 魔法C+ 侧面命中自愈
                "因果律预测", // 2  4BP 群体回避↑ 会心↑ + 因果律
                "水晶结界",   // 3  5BP 群体防御↑ + 缓慢再生
                "幻化"        // 4  3BP 开启光环
            };
        }
        else
        {
            return new List<string> {
                "星光射线",   // 0  4BP 直线贯穿 降魔防；命中≥2再降魔攻
                "羽翼之枪",   // 1  2BP 大扇形 光魔法 命中数量自愈
                "因果律预测", // 2  4BP 保留
                "天使赐福",   // 3  5BP 群体攻击↑ + 行动效率↑
                "结束幻化"    // 4  0BP 结束光环
            };
        }
    }

    protected override string GetSkillExtraInfo(int index)
    {
        if (!isTransformed)
        {
            switch (index)
            {
                case 0: return "群体治疗 | BP 4";
                case 1: return "魔法C+ | 侧面命中自愈 | BP 2";
                case 2: return $"群体回避↑ 会心↑ {buffTurnsDefault}回合 + 因果律 | BP 4";
                case 3: return $"结界：群体防御↑ + 再生 {buffTurnsDefault}回合 | BP 5";
                case 4: return $"光环：持续 {auraTurnsDefault} 回合 | BP 3";
            }
        }
        else
        {
            switch (index)
            {
                case 0: return $"直线贯穿 光属性 | 降魔防 | BP 4";
                case 1: return $"大扇形 光属性 | 按命中数自愈 | BP 2";
                case 2: return $"群体回避↑ 会心↑ {buffTurnsDefault}回合 + 因果律 | BP 4";
                case 3: return $"群体攻击↑ + 行动效率↑ {buffTurnsDefault}回合 | BP 5";
                case 4: return "结束幻化 | BP 0";
            }
        }
        return null;
    }

    protected override float GetSkillCastRange(int index)
    {
        // 可按技能不同返回不同半径，这里使用通用显示范围
        return targetSelectionRange;
    }

    protected override bool TryQuickCastSkill(int index)
    {
        var tm = FindObjectOfType<BattleTurnManager>();
        if (tm == null) return false;

        // 计算当前技能的消耗
        int cost = GetBpCost(index);
        if (!tm.CanSpendBattlePoints(cost)) { sfxPlayer?.Play("Error"); skillReselectRequested = true; return false; }

        bool ok = false;
        if (!isTransformed)
        {
            switch (index)
            {
                case 0: ok = Skill_PureHeal(tm); break;
                case 1: ok = Skill_PhaseBlade(tm); break;
                case 2: ok = Skill_CausalityPrediction(tm); break;
                case 3: ok = Skill_CrystalBarrier(tm); break;
                case 4: ok = Skill_Transform(tm); break;
            }
        }
        else
        {
            switch (index)
            {
                case 0: ok = Skill_StarlightRay(tm); break;
                case 1: ok = Skill_WingSpear(tm); break;
                case 2: ok = Skill_CausalityPrediction(tm); break;
                case 3: ok = Skill_AngelBlessing(tm); break;
                case 4: ok = Skill_EndTransform(); break;
            }
        }

        if (ok)
        {
            // 消耗点数（结束幻化为0不消耗）
            if (cost > 0 && !tm.TrySpendBattlePoints(cost)) { sfxPlayer?.Play("Error"); return false; }
            return true;
        }
        return false;
    }

    private int GetBpCost(int index)
    {
        if (!isTransformed)
        {
            switch (index)
            {
                case 0: return 4; // 纯白疗愈
                case 1: return 2; // 虚化刃
                case 2: return 4; // 因果律预测
                case 3: return 5; // 水晶结界
                case 4: return 3; // 幻化
            }
        }
        else
        {
            switch (index)
            {
                case 0: return 4; // 星光射线
                case 1: return 2; // 羽翼之枪
                case 2: return 4; // 因果律预测
                case 3: return 5; // 天使赐福
                case 4: return 0; // 结束幻化
            }
        }
        return 0;
    }

    // ====== 技能实现 ======

    // 纯白疗愈：群体治疗
    private bool Skill_PureHeal(BattleTurnManager tm)
    {
        foreach (var ally in GetAllies())
        {
            int amount = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * healGroupPower));
            skillSystem?.Heal(ally, amount);
        }
        indicatorManager?.ClearIndicatorsByTag(BattleIndicatorManager.Tags.SkillPreview);
        return true;
    }

    // 虚化刃：魔法C+ 侧面命中自愈
    private bool Skill_PhaseBlade(BattleTurnManager tm)
    {
        // 近战目标
        BattleUnit target = FindTargetMelee();
        if (target == null) { sfxPlayer?.Play("Error"); skillReselectRequested = true; return false; }

        int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * bladePowerCPlus));
        skillSystem?.CauseDamage(target, unit, dmg, DamageType.Magic);

        if (IsSide(target))
        {
            int heal = Mathf.RoundToInt(dmg * bladeSideHealRatio);
            skillSystem?.Heal(unit, heal);
        }
        return true;
    }

    // 因果律预测：群体回避↑ 会心↑ + 因果律
    private bool Skill_CausalityPrediction(BattleTurnManager tm)
    {
        foreach (var ally in GetAllies())
        {
            ApplyTimedBuff_EvasionCrit(ally, evasionUpAmount, critUpAmount, buffTurnsDefault);
            // 因果律：必定回避一次攻击（由 SkillSystem.CauseDamage 处理）
            ally.causality = Mathf.Max(ally.causality, 1);
            skillSystem?.ShowPopup("因果律", ally.transform.position, Color.yellow);
        }
        return true;
    }

    // 水晶结界：群体防御↑ + 再生
    private bool Skill_CrystalBarrier(BattleTurnManager tm)
    {
        foreach (var ally in GetAllies())
        {
            ApplyTimedBuff_DefenseRegen(ally, defenseUpAmount, Mathf.Max(1, Mathf.RoundToInt(ally.battleMaxHp * regenRatioPerTurn)), buffTurnsDefault);
            skillSystem?.ShowPopup("结界", ally.transform.position, new Color(0.6f, 0.9f, 1f));
        }
        return true;
    }

    // 幻化：开启光环
    private bool Skill_Transform(BattleTurnManager tm)
    {
        isTransformed = true;
        transformTurnsLeft = auraTurnsDefault;
        skillSystem?.ShowPopup("幻化", unit.transform.position, new Color(1f, 0.85f, 0.4f));
        return true;
    }

    // 结束幻化
    private bool Skill_EndTransform()
    {
        isTransformed = false;
        transformTurnsLeft = 0;
        skillSystem?.ShowPopup("结束幻化", unit.transform.position, Color.white);
        return true;
    }

    // 幻化后：羽翼之枪（大扇形，命中数自愈）
    private bool Skill_WingSpear(BattleTurnManager tm)
    {
        int hit = 0;
        Vector3 origin = transform.position;
        Vector3 forward = GetCameraForwardOnXZ();

        foreach (var enemy in GetEnemies())
        {
            Vector3 v = enemy.transform.position - origin; v.y = 0f;
            if (v.magnitude <= spearRadius)
            {
                float ang = Vector3.Angle(forward, v.normalized);
                if (ang <= spearAngle * 0.5f)
                {
                    int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * spearPower));
                    skillSystem?.CauseDamage(enemy, unit, dmg, DamageType.Magic);
                    hit++;
                }
            }
        }

        if (hit > 0)
        {
            int heal = Mathf.Max(1, Mathf.RoundToInt(unit.battleMaxHp * spearSelfHealPerTargetRatio * hit));
            skillSystem?.Heal(unit, heal);
        }
        return true;
    }

    // 幻化后：天使赐福（群体攻击↑ + 行动效率↑）
    private bool Skill_AngelBlessing(BattleTurnManager tm)
    {
        foreach (var ally in GetAllies())
        {
            ApplyTimedBuff_AttackSpeed(ally, attackUpAmount, speedUpAmount, buffTurnsDefault);
            skillSystem?.ShowPopup("赐福", ally.transform.position, new Color(1f, 0.9f, 0.6f));
        }
        return true;
    }

    // 幻化后：星光射线（直线贯穿，降魔防；命中多个目标再降魔攻）
    private bool Skill_StarlightRay(BattleTurnManager tm)
    {
        Vector3 o = transform.position + Vector3.up * 0.5f;
        Vector3 fwd = GetCameraForwardOnXZ().normalized;
        int hit = 0;

        foreach (var enemy in GetEnemies())
        {
            // 距离直线的最短距离（到射线的点距 <= 半宽，且在线段投影范围内）
            Vector3 toTarget = enemy.transform.position - o;
            float t = Vector3.Dot(toTarget, fwd);
            if (t < 0 || t > starlightLength) continue;
            Vector3 closest = o + fwd * t;
            float dist = Vector3.Distance(enemy.transform.position, closest);
            if (dist <= starlightWidth)
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * 1.3f));
                skillSystem?.CauseDamage(enemy, unit, dmg, DamageType.Magic);
                // 降魔防
                ApplyTimedDebuff_MagicDef(enemy, -Mathf.Abs(magicDefDownAmount), buffTurnsDefault);
                hit++;
            }
        }

        if (hit >= 2)
        {
            foreach (var enemy in GetEnemies())
            {
                Vector3 toTarget = enemy.transform.position - o;
                float t = Vector3.Dot(toTarget, fwd);
                if (t < 0 || t > starlightLength) continue;
                Vector3 closest = o + fwd * t;
                float dist = Vector3.Distance(enemy.transform.position, closest);
                if (dist <= starlightWidth)
                {
                    ApplyTimedDebuff_MagicAtk(enemy, -Mathf.Abs(magicAtkDownAmount), buffTurnsDefault);
                }
            }
        }
        return true;
    }

    // ====== Buff 应用（写入 BattleUnit 字段，由 SkillSystem.OnUnitTurnStart 统一结算/回退） ======
    private void ApplyTimedBuff_EvasionCrit(BattleUnit u, int evasionAdd, int critAdd, int turns)
    {
        u.battleSpdDef += evasionAdd; u.deltaSpdDef += evasionAdd; u.buffTurns_EvasionUp = Mathf.Max(u.buffTurns_EvasionUp, turns);
        u.battleCri += critAdd; u.deltaCri += critAdd; u.buffTurns_CritUp = Mathf.Max(u.buffTurns_CritUp, turns);
    }

    private void ApplyTimedBuff_DefenseRegen(BattleUnit u, int defAdd, int regenPerTurn, int turns)
    {
        u.battleDef += defAdd; u.deltaDef += defAdd; u.buffTurns_DefUp = Mathf.Max(u.buffTurns_DefUp, turns);
        u.regenPerTurn = Mathf.Max(u.regenPerTurn, regenPerTurn);
        u.buffTurns_Regen = Mathf.Max(u.buffTurns_Regen, turns);
    }

    private void ApplyTimedBuff_AttackSpeed(BattleUnit u, int atkAdd, int spdAdd, int turns)
    {
        u.battleAtk += atkAdd; u.deltaAtk += atkAdd; u.buffTurns_AttackUp = Mathf.Max(u.buffTurns_AttackUp, turns);
        u.battleSpd += spdAdd; u.deltaSpd += spdAdd; u.buffTurns_SpdUp = Mathf.Max(u.buffTurns_SpdUp, turns);
    }

    private void ApplyTimedDebuff_MagicDef(BattleUnit u, int magicDefDelta, int turns)
    {
        u.battleMagicDef += magicDefDelta; u.deltaMagicDef += magicDefDelta;
        u.debuffTurns_MagicDefDown = Mathf.Max(u.debuffTurns_MagicDefDown, turns);
    }

    private void ApplyTimedDebuff_MagicAtk(BattleUnit u, int magicAtkDelta, int turns)
    {
        u.battleMagicAtk += magicAtkDelta; u.deltaMagicAtk += magicAtkDelta;
        u.debuffTurns_MagicAtkDown = Mathf.Max(u.debuffTurns_MagicAtkDown, turns);
    }

    // ====== 工具 ======
    private IEnumerable<BattleUnit> GetAllies()
    {
        foreach (var u in FindObjectsOfType<BattleUnit>())
            if (u != null && u.unitType == unit.unitType) yield return u;
    }

    private IEnumerable<BattleUnit> GetEnemies()
    {
        foreach (var u in FindObjectsOfType<BattleUnit>())
            if (u != null && u.unitType != unit.unitType) yield return u;
    }

    private BattleUnit FindTargetMelee()
    {
        Camera cam = Camera.main;
        BattleUnit target = RaycastUnitUnderCursor(cam);
        if (target == null) target = FindNearestInRange(meleeRange);
        if (target == null || !IsValidTarget(target) || Vector3.Distance(transform.position, target.transform.position) > meleeRange) return null;
        return target;
    }

    private Vector3 GetCameraForwardOnXZ()
    {
        Camera cam = Camera.main;
        Vector3 fwd = (cam != null ? cam.transform.forward : transform.forward);
        fwd.y = 0f;
        return fwd.sqrMagnitude > 0.001f ? fwd.normalized : transform.forward;
    }

    // 复用 Fayt 的检测
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
}