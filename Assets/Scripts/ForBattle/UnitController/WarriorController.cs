using System.Collections;
using UnityEngine;
using Assets.Scripts.ForBattle;

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

    protected override IEnumerator ExecuteSkill()
    {
        // 显示技能菜单供玩家选择
        Debug.Log("[WarriorController] 选择技能: 1-斩击(目标) 2-旋风斩(区域)");

        if (battleUI != null)
            battleUI.UpdatePrompt("选择技能: 1-斩击 2-旋风斩");

        int skillChoice = 0;
        bool skillSelected = false;

        while (!skillSelected)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                skillChoice = 1;
                skillSelected = true;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                skillChoice = 2;
                skillSelected = true;
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                Debug.Log("取消技能");
                yield break;
            }

            yield return null;
        }

        switch (skillChoice)
        {
            case 1:
                yield return SkillSlash();
                break;
            case 2:
                yield return SkillWhirlwind();
                break;
        }
    }

    /// <summary>
    /// 技能1：斩击 - 对单一目标造成高额伤害（使用目标选择）
    /// </summary>
    private IEnumerator SkillSlash()
    {
        Debug.Log("[WarriorController] 使用斩击");

        // 使用基类提供的目标选择
        yield return UseTargetSelection((target) =>
        {
            if (target != null && skillSystem != null)
            {
                Debug.Log($"斩击命中: {target.unitName}");
                skillSystem.CauseDamage(target, slashDamage, DamageType.Physics);

                // TODO: 播放斩击动画与特效
            }
        });

        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>
    /// 技能2：旋风斩 - 对区域内所有敌人造成伤害（使用区域选择）
    /// </summary>
    private IEnumerator SkillWhirlwind()
    {
        Debug.Log("[WarriorController] 使用旋风斩");

        // 使用基类提供的区域选择
        yield return UseAreaSelection(whirlwindRadius, (area) =>
        {
            Debug.Log($"旋风斩在 {area} 爆发");

            // 查找区域内的所有敌方单位
            BattleUnit[] allUnits = FindObjectsOfType<BattleUnit>();
            int hitCount = 0;

            foreach (var u in allUnits)
            {
                if (u == null || u == unit) continue;

                // 只攻击敌方
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
            // TODO: 播放旋风特效
        });

        yield return new WaitForSeconds(0.8f);
    }
}
