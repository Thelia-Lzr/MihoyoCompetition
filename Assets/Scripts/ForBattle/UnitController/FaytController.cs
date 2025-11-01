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
    public float powerC =0.8f; // 撕裂连击（每段）
    public float powerB =1.2f; // 背后斩击
    public float powerBPlus =1.4f; // 流水横扫
    public float powerAPlus =1.8f; //轮转强袭

    [Header("Skill Costs (BP)")]
    public int costBackstab =3; // 背后斩击
    public int costRendCombo =2; // 撕裂连击
    public int costShadowEye =3; // 暗影之眼
    public int costWaterSweep =5; // 流水横扫
    public int costRotaryAssault =6;//轮转强袭
    public int costLateAdvantage =0;// 后发优势

    [Header("AoE Settings")]
    public float waterSweepRadius =4.5f; // 扇形半径
    public float waterSweepAngle =90f; // 扇形角度

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
            case 0: return $"威力B x{powerB:F2} | BP {costBackstab}";
            case 1: return $"威力C x{powerC:F2} | BP {costRendCombo}";
            case 2: return $"辅助 | BP {costShadowEye}";
            case 3: return $"威力B+ x{powerBPlus:F2} | BP {costWaterSweep}";
            case 4: return $"威力A+ x{powerAPlus:F2} | BP {costRotaryAssault}";
            case 5: return $"辅助 | BP {costLateAdvantage}";
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

    protected override void ShowSkillPreview(int index)
    {
        if (indicatorManager == null) return;
        indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.SkillPreview);

        var tm = FindObjectOfType<BattleTurnManager>();
        bool enough = true;
        if (tm != null)
        {
            int c = GetCostByIndex(index);
            enough = tm.CanSpendBattlePoints(c);
        }

        if (index ==0 || index ==1 || index ==4)
        {
            // 单体近战：高亮最近有效目标（近战范围内）
            BattleUnit target = FindNearestInRange(meleeRange);
            if (target != null)
            {
                indicatorManager.CreateTargetMarker(target.transform, true);
            }
        }
        else if (index ==3)
        {
            // 扇形范围：以玩家为圆心，朝向根据镜头方向实时变化
            var sector = indicatorManager.CreateSectorIndicator(
                transform,
                waterSweepRadius,
                waterSweepAngle,
                BattleIndicatorManager.Tags.SkillPreview,
                true);

            //取摄像机前向并投影到水平面作为朝向
            Camera cam = Camera.main ?? (Camera.allCamerasCount >0 ? Camera.allCameras[0] : null);
            if (cam != null)
            {
                Vector3 camFwd = cam.transform.forward; camFwd.y =0f;
                if (camFwd.sqrMagnitude >0.001f)
                {
                    indicatorManager.UpdateSectorRotation(sector, transform, camFwd.normalized);
                }
            }
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
        float dot = Vector3.Dot(target.transform.forward, toAttacker);
        // >0.5约等于在背后60 度扇形内
        return dot >0.5f;
    }

    private bool IsSide(BattleUnit target)
    {
        Vector3 toAttacker = (transform.position - target.transform.position).normalized;
        float dot = Mathf.Abs(Vector3.Dot(target.transform.right, toAttacker));
        // 与左右方向接近，认为侧面
        return dot >0.7f;
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
    protected override bool TryQuickCastSkill(int index)
    {
        var tm = FindObjectOfType<BattleTurnManager>();
        int cost = GetCostByIndex(index);
        if (tm != null && !tm.CanSpendBattlePoints(cost))
        {
            if (sfxPlayer != null) sfxPlayer.Play("Error");
            skillReselectRequested = true;
            return false;
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
                    return false;
                }
                if (tm != null && !tm.TrySpendBattlePoints(cost)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; return false; }
                int dmg = CalcDamage(powerB);
                if (IsBehind(target)) dmg *=2;
                skillSystem?.CauseDamage(target, dmg, DamageType.Physics);
                skillReselectRequested = false;
                return true;
            }
            case 1: // 撕裂连击：单体近战，两段，侧面额外提升（以伤害加作为例）
            {
                BattleUnit target = RaycastUnitUnderCursor(cam);
                if (target == null) target = FindNearestInRange(meleeRange);
                if (target == null || !IsValidTarget(target) || Vector3.Distance(transform.position, target.transform.position) > meleeRange)
                {
                    if (sfxPlayer != null) sfxPlayer.Play("Error");
                    skillReselectRequested = true;
                    return false;
                }
                if (tm != null && !tm.TrySpendBattlePoints(cost)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; return false; }
                float bonus = IsSide(target) ?1.25f :1f;
                int hit1 = Mathf.RoundToInt(CalcDamage(powerC) * bonus);
                int hit2 = Mathf.RoundToInt(CalcDamage(powerC) * bonus);
                skillSystem?.CauseDamage(target, hit1, DamageType.Physics);
                skillSystem?.CauseDamage(target, hit2, DamageType.Physics);
                skillReselectRequested = false;
                return true;
            }
            case 2: // 暗影之眼：自身增益与隐身（这里以日志提示为主）
            {
                if (tm != null && !tm.TrySpendBattlePoints(cost)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; return false; }
                Debug.Log("[Fayt] 暗影之眼：攻击上升并获得隐身（占位实现）");
                // TODO: 接入状态系统：提高攻击、设置隐身标记
                skillReselectRequested = false;
                return true;
            }
            case 3: // 流水横扫：扇形范围群体伤害并降防
            {
                if (tm != null && !tm.TrySpendBattlePoints(cost)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; return false; }
                int applied =0;
                Vector3 origin = transform.position;
                // 使用镜头方向作为朝向（使用外层 cam变量）
                Vector3 forward = transform.forward;
                if (cam != null)
                {
                    Vector3 camFwd = cam.transform.forward; camFwd.y =0f;
                    if (camFwd.sqrMagnitude >0.001f) forward = camFwd.normalized;
                }
                foreach (var u in FindObjectsOfType<BattleUnit>())
                {
                    if (!IsValidTarget(u)) continue;
                    Vector3 dir = (u.transform.position - origin); dir.y =0f;
                    if (dir.magnitude <= waterSweepRadius)
                    {
                        float ang = Vector3.Angle(forward, dir.normalized);
                        if (ang <= waterSweepAngle *0.5f)
                        {
                            skillSystem?.CauseDamage(u, CalcDamage(powerBPlus), DamageType.Physics);
                            // 简易降防（占位）：降低10 点物防
                            u.battleDef = Mathf.Max(0, u.battleDef -10);
                            applied++;
                        }
                    }
                }
                Debug.Log($"[Fayt] 流水横扫 命中 {applied} 个目标");
                skillReselectRequested = false;
                return true;
            }
            case 4: //轮转强袭：单体近战大伤害
            {
                BattleUnit target = RaycastUnitUnderCursor(cam);
                if (target == null) target = FindNearestInRange(meleeRange);
                if (target == null || !IsValidTarget(target) || Vector3.Distance(transform.position, target.transform.position) > meleeRange)
                {
                    if (sfxPlayer != null) sfxPlayer.Play("Error");
                    skillReselectRequested = true;
                    return false;
                }
                if (tm != null && !tm.TrySpendBattlePoints(cost)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; return false; }
                skillSystem?.CauseDamage(target, CalcDamage(powerAPlus), DamageType.Physics);
                skillReselectRequested = false;
                return true;
            }
            case 5: // 后发优势：损失一半生命，回复4BP
            {
                if (tm != null)
                {
                    int half = Mathf.Max(1, unit.battleMaxHp /2);
                    unit.battleHp = Mathf.Max(1, unit.battleHp - half);
                    tm.AddBattlePoints(4);
                    skillReselectRequested = false;
                    return true;
                }
                if (sfxPlayer != null) sfxPlayer.Play("Error");
                skillReselectRequested = true;
                return false;
            }
        }
        return false;
    }

    private BattleUnit RaycastUnitUnderCursor(Camera cam)
    {
        if (cam == null) return null;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit,100f))
        {
            return hit.collider.GetComponentInParent<BattleUnit>();
        }
        return null;
    }

    protected override IEnumerator ExecuteSkillByIndex(int index)
    {
        switch (index)
        {
            case 0: yield return SkillBackstab(); break;
            case 1: yield return SkillRendCombo(); break;
            case 2: yield return SkillShadowEye(); break;
            case 3: yield return SkillWaterSweep(); break;
            case 4: yield return SkillRotaryAssault(); break;
            case 5: yield return SkillLateAdvantage(); break;
            default:
                Debug.LogWarning($"FaytController: invalid skill index {index}");
                break;
        }
    }

    private IEnumerator SkillBackstab()
    {
        var tm = FindObjectOfType<BattleTurnManager>();
        if (tm != null && !tm.CanSpendBattlePoints(costBackstab)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
        yield return UseTargetSelection(meleeRange, (target) =>
        {
            if (target == null) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; return; }
            if (tm != null && !tm.TrySpendBattlePoints(costBackstab)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; return; }
            int dmg = CalcDamage(powerB);
            if (IsBehind(target)) dmg *=2;
            skillSystem?.CauseDamage(target, dmg, DamageType.Physics);
        });
        yield return new WaitForSeconds(0.4f);
    }

    private IEnumerator SkillRendCombo()
    {
        var tm = FindObjectOfType<BattleTurnManager>();
        if (tm != null && !tm.CanSpendBattlePoints(costRendCombo)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
        yield return UseTargetSelection(meleeRange, (target) =>
        {
            if (target == null) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; return; }
            if (tm != null && !tm.TrySpendBattlePoints(costRendCombo)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; return; }
            float bonus = IsSide(target) ?1.25f :1f;
            int hit1 = Mathf.RoundToInt(CalcDamage(powerC) * bonus);
            int hit2 = Mathf.RoundToInt(CalcDamage(powerC) * bonus);
            skillSystem?.CauseDamage(target, hit1, DamageType.Physics);
            skillSystem?.CauseDamage(target, hit2, DamageType.Physics);
        });
        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator SkillShadowEye()
    {
        var tm = FindObjectOfType<BattleTurnManager>();
        if (tm != null && !tm.TrySpendBattlePoints(costShadowEye)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
        Debug.Log("[Fayt] 暗影之眼：攻击上升并获得隐身（占位实现）");
        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator SkillWaterSweep()
    {
        var tm = FindObjectOfType<BattleTurnManager>();
        if (tm != null && !tm.CanSpendBattlePoints(costWaterSweep)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }

        // 使用方向选择来确定扇形朝向
        yield return UseDirectionSelection((dir) =>
        {
            if (tm != null && !tm.TrySpendBattlePoints(costWaterSweep)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; return; }
            int applied =0;
            Vector3 origin = transform.position;
            foreach (var u in FindObjectsOfType<BattleUnit>())
            {
                if (!IsValidTarget(u)) continue;
                Vector3 v = (u.transform.position - origin); v.y =0f;
                if (v.magnitude <= waterSweepRadius)
                {
                    float ang = Vector3.Angle(dir, v.normalized);
                    if (ang <= waterSweepAngle *0.5f)
                    {
                        skillSystem?.CauseDamage(u, CalcDamage(powerBPlus), DamageType.Physics);
                        u.battleDef = Mathf.Max(0, u.battleDef -10);
                        applied++;
                    }
                }
            }
            Debug.Log($"[Fayt] 流水横扫 命中 {applied} 个目标");
        });
        yield return new WaitForSeconds(0.6f);
    }

    private IEnumerator SkillRotaryAssault()
    {
        var tm = FindObjectOfType<BattleTurnManager>();
        if (tm != null && !tm.CanSpendBattlePoints(costRotaryAssault)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
        yield return UseTargetSelection(meleeRange, (target) =>
        {
            if (target == null) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; return; }
            if (tm != null && !tm.TrySpendBattlePoints(costRotaryAssault)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; return; }
            skillSystem?.CauseDamage(target, CalcDamage(powerAPlus), DamageType.Physics);
        });
        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator SkillLateAdvantage()
    {
        var tm = FindObjectOfType<BattleTurnManager>();
        if (tm == null) yield break;
        int half = Mathf.Max(1, unit.battleMaxHp /2);
        unit.battleHp = Mathf.Max(1, unit.battleHp - half);
        tm.AddBattlePoints(4);
        Debug.Log("[Fayt] 后发优势：损失一半生命，回复4BP");
        yield return new WaitForSeconds(0.3f);
    }
}
