using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.ForBattle.UI;
using Assets.Scripts.ForBattle;
using Assets.Scripts.ForBattle.Indicators;  // 添加这个 using

/// <summary>
/// 示例特化角色控制器：展示如何继承 PlayerController 并实现特殊技能
/// </summary>
public class WarriorController : PlayerController
{
    [Header("Warrior Skills")]
    public int slashDamage = 50;
    public float slashRange = 5f;
    public int whirlwindDamage = 30;
    public float whirlwindRadius = 3f;
    [Header("BP Costs")]
    public int whirlwindCost = 3;

    private List<string> warriorSkillNames = new List<string> { "斩击", "旋风斩" };

    // 不再需要手动跟踪预览指示器，使用标签系统自动管理

    protected override List<string> GetSkillNames()
    {
        return warriorSkillNames;
    }

    protected override IEnumerator ExecuteSkillByIndex(int index)
    {
        switch (index)
        {
            case 0:
                yield return SkillSlash();
                break;
            case 1:
                yield return SkillWhirlwind();
                break;
            default:
                Debug.LogWarning($"WarriorController: invalid skill index {index}");
                break;
        }
    }

    // Single-click quick cast implementation
    protected override bool TryQuickCastSkill(int index)
    {
        if (index == 0)
        {
            // Quick cast Slash: require a valid enemy target in slashRange
            Camera cam = Camera.main;
            BattleUnit target = null;
            if (cam != null)
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    target = hit.collider.GetComponentInParent<BattleUnit>();
                }
                if (target == null)
                {
                    Ray centerRay = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));
                    if (Physics.Raycast(centerRay, out RaycastHit ch, 100f))
                        target = ch.collider.GetComponentInParent<BattleUnit>();
                }
            }
            if (target == null)
            {
                // fallback nearest
                float best = float.MaxValue; BattleUnit nearest = null;
                foreach (var u in FindObjectsOfType<BattleUnit>())
                {
                    if (!IsValidTarget(u)) continue;
                    float d = Vector3.Distance(transform.position, u.transform.position);
                    if (d <= slashRange && d < best) { best = d; nearest = u; }
                }
                target = nearest;
            }
            // enforce melee range for selective skill and normal attack (use meleeRange)
            if (target != null && IsValidTarget(target) && Vector3.Distance(transform.position, target.transform.position) <= meleeRange)
            {
                if (skillSystem != null)
                {
                    skillSystem.CauseDamage(target, slashDamage, DamageType.Physics);
                    skillReselectRequested = false;
                    return true;
                }
            }
            // keep selecting if no valid target
            if (sfxPlayer != null) sfxPlayer.Play("Error");
            skillReselectRequested = true;
            return false;
        }
        else if (index == 1)
        {
            // Quick cast Whirlwind: require BP and a valid ground point under mouse within targetSelectionRange
            var tm = FindObjectOfType<BattleTurnManager>();
            if (tm != null && !tm.CanSpendBattlePoints(whirlwindCost))
            {
                if (sfxPlayer != null) sfxPlayer.Play("Error");
                skillReselectRequested = true;
                return false;
            }
            Camera cam = Camera.main;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!RaycastIgnoreUnits(ray, out RaycastHit hit, 100f)) return false;
            Vector3 pos = GetGroundPosition(hit.point);
            if (Vector3.Distance(transform.position, pos) > targetSelectionRange) return false;
            // spend BP and apply
            if (tm != null && !tm.TrySpendBattlePoints(whirlwindCost))
            {
                if (sfxPlayer != null) sfxPlayer.Play("Error");
                skillReselectRequested = true;
                return false;
            }
            int hitCount = 0;
            foreach (var u in FindObjectsOfType<BattleUnit>())
            {
                if (u == null || u == unit) continue;
                if (unit.unitType == BattleUnitType.Player && u.unitType == BattleUnitType.Enemy)
                {
                    if (Vector3.Distance(pos, u.transform.position) <= whirlwindRadius)
                    {
                        if (skillSystem != null)
                        {
                            skillSystem.CauseDamage(u, whirlwindDamage, DamageType.Physics);
                            hitCount++;
                        }
                    }
                }
            }
            Debug.Log($"旋风斩命中 {hitCount} 个目标 @ {pos}");
            skillReselectRequested = false;
            return true;
        }
        return false;
    }

    protected override void ShowSkillPreview(int index)
    {
        if (indicatorManager == null) return;

        // 清理之前的技能预览（通过标签自动管理）
        indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.SkillPreview);

        Camera cam = Camera.main;

        if (index == 0)  // 斩击
        {
            BattleUnit preview = null;
            if (cam != null)
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    preview = hit.collider.GetComponentInParent<BattleUnit>();
                }
                if (preview == null)
                {
                    Ray centerRay = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));
                    if (Physics.Raycast(centerRay, out RaycastHit ch, 100f))
                        preview = ch.collider.GetComponentInParent<BattleUnit>();
                }
            }

            if (preview != null && IsValidTarget(preview) && Vector3.Distance(transform.position, preview.transform.position) <= meleeRange)
            {
                //目标标记默认清除之前的，避免重复
                indicatorManager.CreateTargetMarker(preview.transform, true);
            }
            else
            {
                BattleUnit nearest = null;
                float best = float.MaxValue;
                foreach (var u in FindObjectsOfType<BattleUnit>())
                {
                    if (!IsValidTarget(u)) continue;
                    float d = Vector3.Distance(transform.position, u.transform.position);
                    if (d <= meleeRange && d < best)
                    {
                        best = d; nearest = u;
                    }
                }
                if (nearest != null)
                {
                    indicatorManager.CreateTargetMarker(nearest.transform, true);
                }
            }
        }
        else if (index == 1)  // 旋风斩
        {
            if (cam != null)
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (RaycastIgnoreUnits(ray, out RaycastHit hit, 100f))
                {
                    Vector3 pos = GetGroundPosition(hit.point);
                    // 同时考虑距离与战技点是否足够
                    bool inRange = Vector3.Distance(transform.position, pos) <= targetSelectionRange;
                    bool enoughBP = true;
                    var tmPrev = FindObjectOfType<BattleTurnManager>();
                    if (tmPrev != null)
                    {
                        enoughBP = tmPrev.CanSpendBattlePoints(whirlwindCost);
                    }
                    bool valid = inRange && enoughBP;

                    // 使用 SkillPreview 标签，自动清理同标签旧指示器（仍然使用圆形表现）
                    indicatorManager.CreateCircleIndicator(
                 pos,
             whirlwindRadius,
                 valid,
                false,  // 填充圆
                BattleIndicatorManager.Tags.SkillPreview,  // 技能预览标签
             true    // 清理同标签旧指示器
               );
                }
            }
        }
    }

    private IEnumerator SkillSlash()
    {
        Debug.Log("[WarriorController] 使用斩击");

        // 技能预览会在父类的 ExecuteTurn 中清理，这里不需要手动清理

        //近战技能：使用近战范围进行目标选择
        yield return UseTargetSelection(meleeRange, (target) =>
          {
              if (target != null && skillSystem != null)
              {
                  Debug.Log($"斩击命中: {target.unitName}");
                  skillSystem.CauseDamage(target, slashDamage, DamageType.Physics);
              }
              else
              {
                  // 未选中有效目标，播放错误提示
                  if (sfxPlayer != null) sfxPlayer.Play("Error");
              }
          });

        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator SkillWhirlwind()
    {
        Debug.Log("[WarriorController] 使用旋风斩");

        //进入范围选择前检查战技点是否足够，不足则播放错误音效并直接返回
        var tm = FindObjectOfType<BattleTurnManager>();
        if (tm != null && !tm.CanSpendBattlePoints(whirlwindCost))
        {
            if (sfxPlayer != null) sfxPlayer.Play("Error");
            // 不进入范围选择，直接结束技能执行（不二次选择）
            yield break;
        }

        // 技能预览会在父类中清理

        yield return UseAreaSelection(whirlwindRadius, (area) =>
         {
             // 再次尝试消耗战技点（防止并发或点数变化）
             if (tm != null && !tm.TrySpendBattlePoints(whirlwindCost))
             {
                 if (sfxPlayer != null) sfxPlayer.Play("Error");
                 // 中止技能结算（不二次选择）
                 return;
             }

             Debug.Log($"旋风斩在 {area} 爆发");

             BattleUnit[] allUnits = FindObjectsOfType<BattleUnit>();
             int hitCount = 0;

             foreach (var u in allUnits)
             {
                 if (u == null || u == unit) continue;

                 if (unit.unitType == BattleUnitType.Player && u.unitType == BattleUnitType.Enemy)
                 {
                     if (Vector3.Distance(area, u.transform.position) <= whirlwindRadius)
                     {
                         if (skillSystem != null)
                         {
                             skillSystem.CauseDamage(u, whirlwindDamage, DamageType.Physics);
                             hitCount++;
                         }
                     }
                 }
             }

             Debug.Log($"旋风斩命中 {hitCount} 个目标");
         });

        yield return new WaitForSeconds(0.8f);
    }

    protected override IEnumerator ExecuteAttack()
    {
        Debug.Log("[WarriorController] 执行自动普攻");

        BattleUnit nearest = null;
        float bestDist = float.MaxValue;
        var units = FindObjectsOfType<BattleUnit>();
        foreach (var u in units)
        {
            if (u == null || u == unit) continue;
            if (unit.unitType == BattleUnitType.Player && u.unitType == BattleUnitType.Enemy)
            {
                float d = Vector3.Distance(transform.position, u.transform.position);
                if (d <= targetSelectionRange && d < bestDist)
                {
                    bestDist = d;
                    nearest = u;
                }
            }
            else if (unit.unitType == BattleUnitType.Enemy && u.unitType == BattleUnitType.Player)
            {
                float d = Vector3.Distance(transform.position, u.transform.position);
                if (d <= targetSelectionRange && d < bestDist)
                {
                    bestDist = d;
                    nearest = u;
                }
            }
        }

        if (nearest != null && skillSystem != null)
        {
            Debug.Log($"普攻命中: {nearest.unitName}");
            skillSystem.CauseDamage(nearest, unit.battleAtk, DamageType.Physics);
            // 普攻成功后积攒战技点
            var tm = FindObjectOfType<BattleTurnManager>();
            if (tm != null)
            {
                tm.AddBattlePoints(tm.pointsPerNormalAttack);
            }
            yield return new WaitForSeconds(0.5f);
            yield break;
        }

        yield return base.ExecuteAttack();
    }

    protected override float GetSkillCastRange(int index)
    {
        if (index ==0) return meleeRange; // Slash is melee
        if (index ==1) return targetSelectionRange; // Whirlwind uses selection range
        return base.GetSkillCastRange(index);
    }
}
