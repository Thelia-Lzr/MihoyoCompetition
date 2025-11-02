using System.Collections;
using UnityEngine;

/// <summary>
/// Base controller component for BattleUnit behavior. Derive from this to implement
/// player-controlled or AI-controlled turn behavior. The TurnManager will call
/// `ExecuteTurn` when it's this unit's turn; the method should yield while the
/// action is playing.
/// </summary>
public abstract class BattleUnitController : MonoBehaviour
{
    public BattleUnit unit;

    // ===== Shared visual/animator utilities for locomotion =====
    [Header("Visual/Animator (Shared)")]
    [Tooltip("Only rotate this child for facing. Leave null to use root transform.")]
    public Transform sharedVisualRoot;
    [Tooltip("Slerp speed for facing rotation.")]
    public float sharedTurnSpeed = 10f;
    [Tooltip("Animator float parameter for locomotion speed.")]
    public string sharedAnimSpeedParam = "Speed";
    [Tooltip("Animator float parameter for Unity-Chan direction.")]
    public string sharedAnimDirectionParam = "Direction";
    [Tooltip("Animator float parameter for StandardAssets forward.")]
    public string sharedAnimForwardParam = "Forward";
    [Tooltip("Animator float parameter for StandardAssets turn.")]
    public string sharedAnimTurnParam = "Turn";
    [Tooltip("Animator bool parameter for grounded state.")]
    public string sharedAnimGroundedParam = "Grounded";
    [Tooltip("Optional animator bool parameter used by some controllers to toggle movement state (e.g., 'IsMoving').")]
    public string sharedAnimMovingBoolParam = "IsMoving";
    [Tooltip("Normalize planar speed (0..1) before sending to animator speed/forward.")]
    public bool sharedNormalizeSpeed = true;
    [Tooltip("Scale value sent to animator speed/forward.")]
    public float sharedAnimSpeedScale = 1f;

    protected Animator sharedAnimator;
    protected bool hasSpeed, hasDirection, hasForward, hasTurn, hasGrounded, hasMovingBool;

    // Called by BattleUnit or external setup to bind the data object
    public virtual void Bind(BattleUnit battleUnit)
    {
        unit = battleUnit;
    }

    // Called when the battle starts for initialization
    public virtual void OnBattleStart() { }

    // Called when the battle ends for cleanup
    public virtual void OnBattleEnd() { }

    // Execute the unit's turn. The TurnManager will StartCoroutine on this.
    // Implementations should yield while animations/effects are playing.
    public abstract IEnumerator ExecuteTurn(BattleTurnManager turnManager);

    // ===== Helpers: Animator mapping and driving =====
    protected void EnsureAnimatorCached()
    {
        if (sharedAnimator != null) return;
        Transform root = sharedVisualRoot != null ? sharedVisualRoot : (unit != null ? unit.transform : transform);
        sharedAnimator = root != null ? root.GetComponentInChildren<Animator>() : null;
        MapSharedAnimatorParameters();
    }

    protected void MapSharedAnimatorParameters()
    {
        hasSpeed = hasDirection = hasForward = hasTurn = hasGrounded = hasMovingBool = false;
        if (sharedAnimator == null) return;
        var ps = sharedAnimator.parameters;
        System.Func<string[], AnimatorControllerParameterType, string> find = (cands, type) =>
        {
            foreach (var c in cands)
            {
                if (string.IsNullOrEmpty(c)) continue;
                foreach (var p in ps)
                {
                    if (p.type == type && p.name == c) return c;
                }
            }
            return null;
        };

        var sp = find(new[] { sharedAnimSpeedParam, "Speed", "Forward", "MoveSpeed", "Velocity", "VelocityZ" }, AnimatorControllerParameterType.Float);
        if (!string.IsNullOrEmpty(sp)) { sharedAnimSpeedParam = sp; hasSpeed = true; }

        var dp = find(new[] { sharedAnimDirectionParam, "Direction", "TurnSpeed", "AngularSpeed" }, AnimatorControllerParameterType.Float);
        if (!string.IsNullOrEmpty(dp)) { sharedAnimDirectionParam = dp; hasDirection = true; }

        var fp = find(new[] { sharedAnimForwardParam, "Forward", "Speed" }, AnimatorControllerParameterType.Float);
        if (!string.IsNullOrEmpty(fp)) { sharedAnimForwardParam = fp; hasForward = true; }

        var tp = find(new[] { sharedAnimTurnParam, "Turn", "Direction" }, AnimatorControllerParameterType.Float);
        if (!string.IsNullOrEmpty(tp)) { sharedAnimTurnParam = tp; hasTurn = true; }

        var gp = find(new[] { sharedAnimGroundedParam, "Grounded", "isGround", "IsGround", "IsGrounded" }, AnimatorControllerParameterType.Bool);
        if (!string.IsNullOrEmpty(gp)) { sharedAnimGroundedParam = gp; hasGrounded = true; }

        var mb = find(new[] { sharedAnimMovingBoolParam, "IsMoving", "Moving", "Walk", "Run", "Move" }, AnimatorControllerParameterType.Bool);
        if (!string.IsNullOrEmpty(mb)) { sharedAnimMovingBoolParam = mb; hasMovingBool = true; }

        sharedAnimator.applyRootMotion = false;
    }

    protected void RotateSharedVisualTowards(Vector3 worldDir, float turnSpeed, float deltaTime)
    {
        if (worldDir.sqrMagnitude < 0.0001f) return;
        var t = sharedVisualRoot != null ? sharedVisualRoot : (unit != null ? unit.transform : transform);
        Vector3 dir = worldDir; dir.y = 0f; if (dir.sqrMagnitude < 0.0001f) return; dir.Normalize();
        Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
        t.rotation = Quaternion.Slerp(t.rotation, target, Mathf.Clamp01(turnSpeed * deltaTime));
    }

    protected void DriveSharedLocomotion(float planarSpeed, Vector3 desiredDir, float referenceMoveSpeed)
    {
        EnsureAnimatorCached();
        if (sharedAnimator == null) return;

        float speedValue = sharedNormalizeSpeed ? (planarSpeed / Mathf.Max(0.0001f, referenceMoveSpeed)) : planarSpeed;
        if (hasSpeed) sharedAnimator.SetFloat(sharedAnimSpeedParam, speedValue * sharedAnimSpeedScale);
        if (hasForward)
        {
            float fwd = sharedNormalizeSpeed ? Mathf.Clamp01(speedValue) : Mathf.Clamp01(planarSpeed / Mathf.Max(0.0001f, referenceMoveSpeed));
            sharedAnimator.SetFloat(sharedAnimForwardParam, fwd * sharedAnimSpeedScale);
        }
        // Direction/Turn
        var t = sharedVisualRoot != null ? sharedVisualRoot : (unit != null ? unit.transform : transform);
        Vector3 curFwd = t.forward; curFwd.y = 0f; if (curFwd.sqrMagnitude > 0f) curFwd.Normalize();
        Vector3 desired = desiredDir; desired.y = 0f; if (desired.sqrMagnitude > 0f) desired.Normalize();
        if (hasDirection)
        {
            float dirVal = 0f;
            if (desired.sqrMagnitude > 0.0001f && curFwd.sqrMagnitude > 0.0001f)
            {
                float ang = Vector3.SignedAngle(curFwd, desired, Vector3.up);
                dirVal = Mathf.Clamp(ang / 120f, -1f, 1f);
            }
            sharedAnimator.SetFloat(sharedAnimDirectionParam, dirVal);
        }
        if (hasTurn)
        {
            float turn = 0f;
            if (desired.sqrMagnitude > 0.0001f && curFwd.sqrMagnitude > 0.0001f)
            {
                float ang = Vector3.SignedAngle(curFwd, desired, Vector3.up);
                turn = Mathf.Clamp(ang / 180f, -1f, 1f);
            }
            sharedAnimator.SetFloat(sharedAnimTurnParam, turn);
        }
        if (hasGrounded) sharedAnimator.SetBool(sharedAnimGroundedParam, true);
        if (hasMovingBool) sharedAnimator.SetBool(sharedAnimMovingBoolParam, planarSpeed > 0.05f);
    }

    // One-frame movement helper for AI: rotates visual, moves position, and drives locomotion.
    protected bool MoveStepTowards(Vector3 targetWorldPos, float moveSpeed, float stopDistance, float deltaTime)
    {
        if (unit == null) return false;
        Vector3 from = unit.transform.position;
        Vector3 to = targetWorldPos;
        Vector3 dir = to - from; dir.y = 0f;
        float dist = dir.magnitude;
        if (dist <= Mathf.Max(0.1f, stopDistance))
        {
            DriveSharedLocomotion(0f, Vector3.zero, moveSpeed);
            return false; // reached
        }
        Vector3 step = (dist > 0.0001f ? dir / dist : Vector3.zero) * (moveSpeed * deltaTime);
        unit.transform.position = from + step;
        unit.battlePos = new Vector2(unit.transform.position.x, unit.transform.position.z);
        RotateSharedVisualTowards(dir, sharedTurnSpeed, deltaTime);
        float planarSpeed = step.magnitude / Mathf.Max(deltaTime, 0.0001f);
        DriveSharedLocomotion(planarSpeed, dir, moveSpeed);
        return true;
    }
}
