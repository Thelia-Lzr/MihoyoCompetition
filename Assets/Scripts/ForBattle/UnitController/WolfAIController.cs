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
    public float moveSpeed = 3.0f;
    [Tooltip("停止距离（近战范围）")]
    public float stopDistance = 1.5f;
    [Tooltip("寻路的最大持续时间（防止卡住）")]
    public float maxChaseTime = 3.0f;

    [Header("Visual/Animation")]
    [Tooltip("只旋转可见模型，不旋转根节点，以免影响相机。")]
    public Transform visualRoot;
    [Tooltip("旋转插值速度")]
    public float turnSpeed = 10f;
    [Tooltip("Animator 浮点参数(速度/BlendTree)")]
    public string animatorSpeedParam = "Speed";
    [Tooltip("Unity-Chan转向参数")]
    public string animatorDirectionParam = "Direction";
    [Tooltip("是否归一化速度到0..1")]
    public bool normalizeAnimatorSpeed = true;
    [Tooltip("速度缩放系数")]
    public float animatorSpeedScale = 1f;
    [Tooltip("可选：Standard Assets Forward/Turn")]
    public string animatorForwardParam = "Forward";
    public string animatorTurnParam = "Turn";
    [Tooltip("可选：落地布尔")]
    public string animatorGroundedParam = "Grounded";
    [Tooltip("可选：移动布尔（IsMoving/Moving/Walk/Run等，用于不使用Speed/Forward的控制器）")]
    public string animatorMovingBoolParam = "IsMoving";

    private SkillSystem skillSystem;

    [Header("Debug")]
    public bool debugAnimator = false;

    public override void OnBattleStart()
    {
        if (skillSystem == null)
        {
            skillSystem = Object.FindObjectOfType<SkillSystem>();
        }

        // bind to shared animator system
        if (sharedVisualRoot == null) sharedVisualRoot = visualRoot;
        // adopt wolf-specific turning speed for shared system
        sharedTurnSpeed = turnSpeed;
        EnsureAnimatorCached();
    }

    public override void OnBattleEnd()
    {
        // ensure locomotion returns to idle
        EnsureAnimatorCached();
        DriveSharedLocomotion(0f, Vector3.zero, moveSpeed);
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
            // 没有找到目标：在狼头顶显示提示飘字
            if (skillSystem == null)
            {
                skillSystem = Object.FindObjectOfType<SkillSystem>();
            }
            if (skillSystem != null)
            {
                skillSystem.ShowPopup("No target", unit != null ? unit.transform.position : transform.position, Color.yellow);
            }
            // 略作停顿后结束回合
            yield return new WaitForSeconds(0.2f);
            yield break;
        }

        //追击并靠近目标
        float elapsed = 0f;
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
                // stop moving, zero speed
                DriveSharedLocomotion(0f, Vector3.zero, moveSpeed);
                break; // 已进入攻击距离
            }

            bool stillMoving = MoveStepTowards(target.transform.position, moveSpeed, stopDistance, Time.deltaTime);
            if (!stillMoving) break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        // stop locomotion on reaching destination or abort conditions
        DriveSharedLocomotion(0f, Vector3.zero, moveSpeed);

        // 攻击前无论如何都面向目标
        if (target != null)
        {
            Vector3 face = target.transform.position - unit.transform.position;
            face.y =0f;
            if (face.sqrMagnitude >0.0001f)
            {
                //立即朝向目标
                var t = sharedVisualRoot != null ? sharedVisualRoot : unit.transform;
                t.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
                //让动画系统知道期望朝向（速度为0，仅用于更新转身参数）
                DriveSharedLocomotion(0f, face.normalized, moveSpeed);
            }
        }

        //造成伤害（等同于自身攻击力）
        if (target != null && skillSystem != null)
        {
            skillSystem.CauseDamage(target, unit, unit.battleAtk, DamageType.Physics);
            sfxPlayer.Play("bite");
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
            if (u.invisible > 0) continue;
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
