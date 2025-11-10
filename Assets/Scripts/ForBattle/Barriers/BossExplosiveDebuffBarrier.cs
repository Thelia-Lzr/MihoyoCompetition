using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.ForBattle;

namespace Assets.Scripts.ForBattle.Barriers
{
    public class BossExplosiveDebuffBarrier : BarrierBase
    {
        // If true, this barrier will explode immediately when the owner next starts their turn
        public bool explodeOnOwnerNextTurnStart = false;

        public int debuffDef = 20;
        public int debuffSpd = 10; // legacy, not used
        public int explodeDamage = 40;

        // track applied speed deltas per unit so we can revert cleanly
        private Dictionary<BattleUnit, int> _appliedSpeedDelta = new Dictionary<BattleUnit, int>();

        protected override string GetColorKey() { return "Poison"; }

        // Apply debuff contribution: reduce def and magicDef
        public override BarrierContribution EvaluateContribution(BattleUnit unit)
        {
            BarrierContribution c = BarrierContribution.Zero;
            c.def = -debuffDef;
            c.magicDef = -debuffDef;
            return c;
        }

        private BattleTurnManager _tmRef;

        protected override void OnEnable()
        {
            base.OnEnable();
            _tmRef = FindObjectOfType<BattleTurnManager>();
            if (_tmRef != null)
            {
                _tmRef.OnUnitTurnStart += HandleUnitTurnStart;
            }
        }

        protected override void Update()
        {
            base.Update();
            // apply or revert speed deltas based on current affected set
            var currentAffected = new HashSet<BattleUnit>();
            foreach (var u in FindObjectsOfType<BattleUnit>())
            {
                if (u == null) continue;
                if (!IsUnitAffected(u)) continue;
                // respect team filter
                if (!AcceptsUnit(u)) continue;
                currentAffected.Add(u);
                if (!_appliedSpeedDelta.ContainsKey(u))
                {
                    // compute 40% of base speed (unit.spd) as integer
                    int delta = Mathf.RoundToInt(u.spd * 0.4f);
                    if (delta != 0)
                    {
                        _appliedSpeedDelta[u] = delta;
                        u.deltaSpd -= delta;
                        u.battleSpd = Mathf.Max(0, u.battleSpd - delta);
                    }
                    else
                    {
                        _appliedSpeedDelta[u] = 0;
                    }
                }
            }

            // find previously applied but no longer affected units -> revert
            var toRemove = new List<BattleUnit>();
            foreach (var kv in _appliedSpeedDelta)
            {
                var u = kv.Key;
                if (u == null) { toRemove.Add(u); continue; }
                if (!currentAffected.Contains(u))
                {
                    int delta = kv.Value;
                    if (delta != 0)
                    {
                        u.deltaSpd += delta;
                        u.battleSpd += delta;
                    }
                    toRemove.Add(u);
                }
            }
            foreach (var r in toRemove) _appliedSpeedDelta.Remove(r);
        }

        private void HandleUnitTurnStart(BattleUnit u)
        {
            if (!explodeOnOwnerNextTurnStart) return;
            if (u == null || owner == null) return;
            if (u == owner)
            {
                // explode now
                Destroy(this.gameObject);
            }
        }

        protected override void OnBarrierTurnEndResolve(BattleUnit unit)
        {
            // nothing special here; speed handled in Update
        }

        protected override void OnDisable()
        {
            // unsubscribe from turn start
            if (_tmRef != null)
            {
                _tmRef.OnUnitTurnStart -= HandleUnitTurnStart;
                _tmRef = null;
            }

            // revert any remaining speed deltas
            foreach (var kv in _appliedSpeedDelta)
            {
                var u = kv.Key; if (u == null) continue;
                int delta = kv.Value;
                if (delta != 0)
                {
                    u.deltaSpd += delta;
                    u.battleSpd += delta;
                }
            }
            _appliedSpeedDelta.Clear();

            // explosion logic: damage all affected units
            var sys = FindObjectOfType<Assets.Scripts.ForBattle.SkillSystem>();
            if (sys != null)
            {
                foreach (var u in FindObjectsOfType<BattleUnit>())
                {
                    if (u == null) continue;
                    if (!IsUnitAffected(u)) continue;
                    sys.CauseDamage(u, owner, explodeDamage, DamageType.Magic);
                }
            }
            base.OnDisable();
        }
    }
}
