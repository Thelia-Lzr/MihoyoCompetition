using UnityEngine;

namespace Assets.Scripts.ForBattle.Barriers
{
    /// <summary>
    /// 水晶结界：
    ///1.处于其中的符合过滤条件的单位获得百分比防御与魔防加成。
    ///2. 在其自己回合结束时若仍在结界中，回复基于最大生命的数值。
    ///颜色键: "crystal"。
    /// </summary>
    public class CrystalBarrier : BarrierBase
    {
        [Header("Crystal Effects")]
        [Tooltip("防御提升百分比")] public float defUpRate = 0.25f;
        [Tooltip("魔防提升百分比")] public float mdefUpRate = 0.25f;
        [Tooltip("回合结束时回复最大生命百分比")] public float healPercentOnTurnEnd = 0.05f;
        [Tooltip("最少回复值 (防止过低)")] public int minHeal = 1;

        protected override string GetColorKey() { return "Crystal"; }

        public override BarrierContribution EvaluateContribution(BattleUnit unit)
        {
            var c = BarrierContribution.Zero;
            // 按当前 battle 防御系数加成（实时刷新，保障与其他加成叠乘顺序简单）
            c.def = Mathf.RoundToInt(unit.battleDef * defUpRate);
            c.magicDef = Mathf.RoundToInt(unit.battleMagicDef * mdefUpRate);
            return c;
        }

        protected override void OnBarrierTurnEndResolve(BattleUnit unit)
        {
            //仅在单位自己回合结束且仍受影响时触发治疗
            if (unit == null) return;
            if (!IsUnitAffected(unit)) return;
            //只治疗 Allies过滤下的友方（若 teamFilter=All也允许）
            if (!AcceptsUnit(unit)) return;
            int heal = Mathf.Max(minHeal, Mathf.RoundToInt(unit.battleMaxHp * healPercentOnTurnEnd));
            if (heal > 0)
            {
                var skillSys = FindObjectOfType<SkillSystem>();
                if (skillSys != null)
                {

                    skillSys.Heal(unit, heal);
                    //skillSys.ShowPopup("Crystal +" + heal, unit.transform.position + Vector3.up * 1.0f, new Color(0.5f, 0.9f, 1f, 1f));
                }
                else
                {
                    //直接加血（无技能系统时）
                    int before = unit.battleHp;
                    unit.battleHp = Mathf.Min(unit.battleMaxHp, unit.battleHp + heal);
                }
            }
        }
    }
}
