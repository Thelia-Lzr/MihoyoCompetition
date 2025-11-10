using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.ForBattle.Indicators;

namespace Assets.Scripts.ForBattle.Barriers
{
    public struct BarrierContribution
    {
        public int atk;
        public int def;
        public int magicAtk;
        public int magicDef;
        public int evasion;
        public int cri;

        public static BarrierContribution Zero => new BarrierContribution();

        public static BarrierContribution Add(BarrierContribution a, BarrierContribution b)
        {
            return new BarrierContribution
            {
                atk = a.atk + b.atk,
                def = a.def + b.def,
                magicAtk = a.magicAtk + b.magicAtk,
                magicDef = a.magicDef + b.magicDef,
                evasion = a.evasion + b.evasion,
                cri = a.cri + b.cri
            };
        }

        public static BarrierContribution Sub(BarrierContribution a, BarrierContribution b)
        {
            return new BarrierContribution
            {
                atk = a.atk - b.atk,
                def = a.def - b.def,
                magicAtk = a.magicAtk - b.magicAtk,
                magicDef = a.magicDef - b.magicDef,
                evasion = a.evasion - b.evasion,
                cri = a.cri - b.cri
            };
        }

        public bool EqualsTo(BarrierContribution other)
        {
            return atk == other.atk && def == other.def && magicAtk == other.magicAtk && magicDef == other.magicDef && evasion == other.evasion && cri == other.cri;
        }
    }

    public enum BarrierTeamFilter
    {
        All,
        Allies,
        Enemies
    }

    /// <summary>
    ///结界基类：派生类通过重写 EvaluateContribution 来定义对单位的加成。
    ///仅使用回合数作为持续：在 owner 的回合结束时递减，减至0 自动销毁。
    /// </summary>
    public abstract class BarrierBase : MonoBehaviour
    {
        [Header("Barrier Settings")]
        [Tooltip("结界半径")] public float radius = 3f;
        [Tooltip("持续回合数（按拥有者回合递减）。默认值为 3 表示持续拥有者的三回合。若设置为 0 则为永久/无限时长。")] public int durationTurns = 3;
        [Tooltip("队伍过滤：All/Allies/Enemies（需要设置 owner）")] public BarrierTeamFilter teamFilter = BarrierTeamFilter.All;
        [Tooltip("结界拥有者（用于 Allies/Enemies 判定与回合计时）")] public BattleUnit owner;

        [Header("Visuals")]
        public bool showIndicator = true;
        public bool hollow = false;
        public string colorKey = "Buff";

        protected BattleIndicatorManager indicatorManager;
        protected GameObject indicator;

        private int _turnsLeft;
        private BattleTurnManager _turnManager;

        private static readonly HashSet<BarrierBase> s_active = new HashSet<BarrierBase>();
        public static IEnumerable<BarrierBase> ActiveBarriers => s_active;

        private bool _indicatorPersistent; // 标记指示器是否为持久（Barrier标签）
        private string _appliedColorKey; //记录已应用的颜色键

        protected virtual string GetColorKey() { return colorKey; }
        protected virtual Color? GetTintColor() { return null; }

        protected virtual void OnEnable()
        {
            s_active.Add(this);
            indicatorManager = FindObjectOfType<BattleIndicatorManager>();
            if (showIndicator && indicatorManager != null)
            {
                Vector3 pos = GetGroundPosition(transform.position);
                var key = GetColorKey();
                _appliedColorKey = key;
                // 使用 Barrier 标签创建，确保在底层
                indicator = indicatorManager.CreateCircleIndicator(
                    pos,
                    radius,
                    true,
                    hollow,
                    BattleIndicatorManager.Tags.Barrier,
                    false,
                    key
                );
                _indicatorPersistent = true;
            }
            // 初始化回合计时
            _turnsLeft = durationTurns;
            _turnManager = FindObjectOfType<BattleTurnManager>();
            if (_turnManager != null)
            {
                _turnManager.OnUnitTurnEnd += HandleUnitTurnEnd;
            }
            // 应用子类自定义颜色
            var tint = GetTintColor();
            if (indicatorManager != null && indicator != null)
            {
                if (tint.HasValue)
                {
                    indicatorManager.RecolorIndicator(indicator, tint.Value);
                }
            }
        }

        protected virtual void OnDisable()
        {
            s_active.Remove(this);
            if (_turnManager != null)
            {
                _turnManager.OnUnitTurnEnd -= HandleUnitTurnEnd;
                _turnManager = null;
            }
            if (indicatorManager != null && indicator != null)
            {
                //仅当对象被真正销毁时才移除指示器 (此处属于生命周期终止)；Persistent 标记不再特殊处理
                indicatorManager.DeleteCircleIndicator(indicator);
                indicator = null;
            }
        }

        private void HandleUnitTurnEnd(BattleUnit unit)
        {
            // 生命周期递减（仅所有者回合）
            if (_turnsLeft > 0 && owner != null && unit == owner)
            {
                // If the owner's turn is an "extra" turn granted by the turn manager, do not consume duration
                if (_turnManager != null && _turnManager.IsActiveExtraTurn(owner))
                {
                    // skip decrement for extra turns
                }
                else
                {
                    _turnsLeft--;
                    if (_turnsLeft <= 0) { Destroy(gameObject); return; }
                }
            }
            // 所有者被销毁则移除
            if (owner == null && durationTurns > 0) { Destroy(gameObject); return; }
            //触发结界回合结束结算：默认仅对仍受影响的单位
            if (unit != null && IsUnitAffected(unit)) OnBarrierTurnEndResolve(unit);
        }

        /// <summary>
        /// 回合结束结算钩子：任意单位回合结束时（并且该单位当前受此结界影响）调用。
        /// 用于实现一次性治疗/伤害/层数衰减等效果。默认不做任何事。
        /// </summary>
        protected virtual void OnBarrierTurnEndResolve(BattleUnit unit) { }

        /// <summary>
        /// 提供给 TurnManager 主动调用的公共封装；在满足影响条件时触发回合结束结算。
        /// </summary>
        public void ForceTurnEndResolve(BattleUnit unit)
        {
            if (unit != null && IsUnitAffected(unit))
            {
                OnBarrierTurnEndResolve(unit);
            }
        }

        protected virtual void Update()
        {
            if (indicatorManager != null && indicator != null)
            {
                Vector3 pos = GetGroundPosition(transform.position);
                //结界使用更低的高度与更薄的厚度/线宽，避免遮挡
                indicatorManager.UpdateCircleIndicatorCustom(indicator, pos, radius, yOffset:0.003f, keepColor:true, solidHeight:0.003f, lineWidth:0.02f);
                // 检测动态颜色键变化
                string keyNow = GetColorKey();
                if (!string.IsNullOrEmpty(keyNow) && keyNow != _appliedColorKey)
                {
                    _appliedColorKey = keyNow;
                    var tint = GetTintColor();
                    if (tint.HasValue)
                    {
                        indicatorManager.RecolorIndicator(indicator, tint.Value);
                    }
                    else
                    {
                        indicatorManager.RebuildColorMap();
                        var fallback = indicatorManager.validColor;
                        indicatorManager.RecolorIndicator(indicator, fallback);
                    }
                }
            }
            if (owner == null && durationTurns > 0)
            {
                Destroy(gameObject);
            }
        }

        protected Vector3 GetGroundPosition(Vector3 src)
        {
            Vector3 origin = src + Vector3.up * 2f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 20f))
            {
                return hit.point + Vector3.up * 0.01f;
            }
            return new Vector3(src.x, 0f + 0.01f, src.z);
        }

        public bool IsUnitInRange(BattleUnit unit)
        {
            if (unit == null) return false;
            return Vector3.Distance(transform.position, unit.transform.position) <= radius;
        }

        public bool AcceptsUnit(BattleUnit unit)
        {
            if (unit == null) return false;
            switch (teamFilter)
            {
                case BarrierTeamFilter.Allies:
                    if (owner == null) return true;
                    return unit.unitType == owner.unitType;
                case BarrierTeamFilter.Enemies:
                    if (owner == null) return true;
                    return unit.unitType != owner.unitType;
                default:
                    return true;
            }
        }

        public virtual bool IsUnitAffected(BattleUnit unit)
        {
            if (!AcceptsUnit(unit)) return false;
            if (!IsUnitInRange(unit)) return false;
            return true;
        }

        public abstract BarrierContribution EvaluateContribution(BattleUnit unit);

        /// <summary>
        /// 在结界内时对技能消耗的调整（正数表示降低BP消耗的点数）。默认0。
        /// </summary>
        public virtual int GetBpCostDelta(BattleUnit unit) { return 0; }

        /// <summary>
        ///统计某单位在所有激活结界下的总BP消耗调整。
        /// </summary>
        public static int GetTotalBpCostDeltaForUnit(BattleUnit unit)
        {
            int sum = 0;
            foreach (var b in ActiveBarriers)
            {
                if (b == null) continue;
                if (!b.IsUnitAffected(unit)) continue;
                sum += Mathf.Max(0, b.GetBpCostDelta(unit));
            }
            return sum;
        }
    }
}
