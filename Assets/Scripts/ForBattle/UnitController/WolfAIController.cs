using System.Collections;
using UnityEngine;
using Assets.Scripts.ForBattle;

/// <summary>
/// 简单的狼型敌人AI控制器：
/// - 在其回合内自动寻找最近的 Player 单位
/// - 朝其移动，直到接近至近战距离
/// -造成等同于自身 battleAtk 的物理伤害
/// </summary>
public class WolfAIController : BattleUnitController
{
 [Header("Movement")]
 public float moveSpeed =3.0f;
 [Tooltip("停止距离（近战范围）")]
 public float stopDistance =1.5f;
 [Tooltip("寻路的最大持续时间（防止卡住）")]
 public float maxChaseTime =3.0f;

 private SkillSystem skillSystem;

 public override void OnBattleStart()
 {
 if (skillSystem == null)
 {
 skillSystem = Object.FindObjectOfType<SkillSystem>();
 }
 }

 public override void OnBattleEnd()
 {
 // no-op
 }

 public override IEnumerator ExecuteTurn(BattleTurnManager turnManager)
 {
 if (unit == null)
 yield break;

 if (skillSystem == null)
 skillSystem = Object.FindObjectOfType<SkillSystem>();

 // 寻找最近的玩家单位
 BattleUnit target = FindNearestPlayer();
 if (target == null)
 {
 // 没有找到目标，略作停顿后结束回合
 yield return new WaitForSeconds(0.2f);
 yield break;
 }

 //追击并靠近目标
 float elapsed =0f;
 while (elapsed < maxChaseTime)
 {
 if (target == null) break; //目标失效
 Vector3 to = target.transform.position;
 Vector3 from = unit.transform.position;
 Vector3 dir = (to - from);
 dir.y =0f;
 float dist = dir.magnitude;
 if (dist <= Mathf.Max(0.1f, stopDistance))
 {
 break; // 已进入攻击距离
 }

 Vector3 step = dir.normalized * moveSpeed * Time.deltaTime;
 if (step.sqrMagnitude >0f)
 {
 unit.transform.position = from + step;
 unit.battlePos = new Vector2(unit.transform.position.x, unit.transform.position.z);
 }

 elapsed += Time.deltaTime;
 yield return null;
 }

 //造成伤害（等同于自身攻击力）
 if (target != null && skillSystem != null)
 {
 skillSystem.CauseDamage(target, unit.battleAtk, DamageType.Physics);
 }

 // 小延迟模拟出招
 yield return new WaitForSeconds(0.4f);
 }

 private BattleUnit FindNearestPlayer()
 {
 BattleUnit nearest = null;
 float best = float.MaxValue;
 var all = Object.FindObjectsOfType<BattleUnit>();
 foreach (var u in all)
 {
 if (u == null || u == unit) continue;
 if (u.unitType != BattleUnitType.Player) continue;
 float d = Vector3.Distance(unit.transform.position, u.transform.position);
 if (d < best)
 {
 best = d;
 nearest = u;
 }
 }
 return nearest;
 }
}
