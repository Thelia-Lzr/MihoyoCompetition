using UnityEngine;

namespace Assets.Scripts.ForBattle.Barriers
{
    /// <summary>
    /// 天使赐福结界：
    ///1.处于其中的符合过滤条件的单位获得攻击力百分比提升。
    ///2.处于其中的单位技能BP消耗减少 fixedBpReduction（最少降到0）。
    ///颜色键: "Angel"。
    /// </summary>
    public class AngelBlessingBarrier : BarrierBase
    {
        [Header("Angel Blessing Effects")]
        [Tooltip("攻击提升百分比")] public float atkUpRate = 0.2f;
        [Tooltip("技能行动点(BP)消耗减少数值")] public int fixedBpReduction = 1;

        protected override string GetColorKey() => "Angel";

        public override BarrierContribution EvaluateContribution(BattleUnit unit)
        {
            var c = BarrierContribution.Zero;
            c.atk = Mathf.RoundToInt(unit.battleAtk * atkUpRate);
            return c;
        }

        public override int GetBpCostDelta(BattleUnit unit)
        {
            return fixedBpReduction; // 正数表示可减少的BP
        }
    }
}
