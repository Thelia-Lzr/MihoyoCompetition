using UnityEngine;
using Assets.Scripts.ForBattle;

namespace Assets.Scripts.ForBattle.Barriers
{
    public class ManaFieldBarrier : BarrierBase
    {
        [Header("Mana Field Settings")]
        public float magicAtkPercent = 0.2f; // 20% magic atk for allies inside
        public int extraMagicLevel = 1; // 在结界中自身魔法等级+1

        protected override string GetColorKey() => "Blue";

        public override BarrierContribution EvaluateContribution(BattleUnit unit)
        {
            var c = BarrierContribution.Zero;
            if (unit == null) return c;
            c.magicAtk = Mathf.RoundToInt(unit.battleMagicAtk * magicAtkPercent);
            return c;
        }

        public override int GetBpCostDelta(BattleUnit unit)
        {
            return 0;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
        }
    }
}
