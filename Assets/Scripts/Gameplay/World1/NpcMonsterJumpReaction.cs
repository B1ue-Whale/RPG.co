using UnityEngine;

/// <summary>
/// Very simple self-preservation reaction: while grounded, if a Monster's estimated
/// time-to-collision with this NPC (horizontal gap / closing speed, from actual
/// current velocities) drops under reactionLeadTime, jump in place. Using time
/// instead of a fixed distance means a monster and NPC running straight at each
/// other trigger the reaction from farther away than a slow approach would, so the
/// jump gets roughly the same amount of real warning either way. Not meant to
/// reliably dodge monsters - it sometimes helps, sometimes still fails, same as a
/// startled hop.
/// <para>
/// Takes control of the motor directly for the duration of the jump via
/// <see cref="NpcCommandPlayback.BeginExternalControl"/>/<c>EndExternalControl</c>,
/// so a recorded jump command can never fire at the same time as this one, and the
/// recording resumes from the exact tick it was suspended at once the NPC lands.
/// </para>
/// </summary>
// Runs after CharacterMotor2D (order 0) so IsGrounded reflects this tick's actual
// position when deciding to trigger or when checking for landing - same reasoning
// as NpcSuspicionController/NpcProgressionController. Runs before NpcSuspicionController
// (order 50) so that if a landing and a Suspicious-trigger coincide on the same tick,
// this reaction has already handed control back before Suspicion decides whether to
// pause, instead of the two overlapping mid-handoff.
[DefaultExecutionOrder(40)]
public class NpcMonsterJumpReaction : MonoBehaviour
{
    [SerializeField] private CharacterMotor2D motor;
    [SerializeField] private NpcCommandPlayback playback;

    [Header("Detection")]
    [Tooltip("Radius searched around this NPC for nearby monsters. Must comfortably cover the worst-case physical distance implied by reactionLeadTime at the fastest closing speed expected in this level, or a fast-closing monster could be time-eligible but outside the search radius and never get evaluated.")]
    [SerializeField] private float detectionRadius = 4.5f;
    [Tooltip("Seconds of warning wanted before an approaching monster would reach this NPC, estimated as horizontal gap / closing speed (from actual current velocities, not just patrol intent). Triggers the reaction earlier when closing speed is high (e.g. both running toward each other) and later when it's low (e.g. NPC standing still), instead of a single fixed distance for every speed.")]
    [SerializeField] private float reactionLeadTime = 0.35f;

    [Header("Reaction")]
    [Tooltip("Minimum seconds between reaction jumps, so landing next to the same monster doesn't cause repeated hops.")]
    [SerializeField] private float reactionCooldown = 0.5f;

    [Header("Dodge Failure Escalation")]
    [Tooltip("Chance [0-1] that a qualifying trigger is ignored entirely (the dodge \"fails\") - the NPC just continues whatever it was doing as if the monster wasn't there. What this run's failure chance escalates from after each successful dodge. Rolled once per trigger, not per tick, and still starts reactionCooldown so a failed roll doesn't just get re-rolled again a moment later against the same monster.")]
    [SerializeField, Range(0f, 1f)] private float baseFailureChance = 0.1f;
    [Tooltip("Percentage points (as a 0-1 fraction) added to the failure chance per successful dodge during the NPC's first run (its life before the first death).")]
    [SerializeField, Range(0f, 1f)] private float run1FailureIncrement = 0.07f;
    [Tooltip("Percentage points (as a 0-1 fraction) added to the failure chance per successful dodge during the NPC's second run (between its first and second death).")]
    [SerializeField, Range(0f, 1f)] private float run2FailureIncrement = 0.03f;

    // 1 = the NPC's first life this session, 2 = after its first death, 3+ = after its
    // second death - from run 3 onward the probabilistic failure roll is disabled
    // entirely (dodge movement can still fail physically, just not this roll).
    private int _runIndex = 1;
    // Successful dodges (rolled and entered the reaction) so far in the current run.
    // Reset to 0 whenever the NPC dies and a new run begins.
    private int _successfulDodgeCount;

    [Header("Gizmos")]
    [Tooltip("Draw the detection radius and worst-case trigger range in the Scene view.")]
    [SerializeField] private bool showDetectionGizmos = true;
    [SerializeField] private Color detectionRadiusColor = new Color(1f, 0.92f, 0.016f, 0.5f);
    [SerializeField] private Color worstCaseRadiusColor = new Color(1f, 0.15f, 0.15f, 0.8f);

    private bool _reacting;
    private bool _hasLeftGround;
    private float _cooldownRemaining;
    private NpcProgressionController _progression;

    private void Awake()
    {
        if (motor == null)
        {
            motor = GetComponent<CharacterMotor2D>();
        }

        if (playback == null)
        {
            playback = GetComponent<NpcCommandPlayback>();
        }

        _progression = GetComponent<NpcProgressionController>();
    }

    /// <summary>
    /// Aborts an in-progress reaction jump and hands the motor back. Used when
    /// death interrupts the hop so the recording is not left in external-control.
    /// </summary>
    public void Cancel()
    {
        if (!_reacting)
        {
            return;
        }

        EndReaction();
    }

    /// <summary>
    /// Called once per NPC death (from NpcProgressionController.DieRoutine, alongside
    /// Cancel()) to advance the dodge-failure escalation to the next run and reset the
    /// successful-dodge count for it. Deliberately separate from Cancel(), which aborts
    /// an in-progress jump and can also run while merely dying, not just on the actual
    /// death transition.
    /// </summary>
    public void OnNpcDied()
    {
        _runIndex++;
        _successfulDodgeCount = 0;
        Debug.Log($"[{nameof(NpcMonsterJumpReaction)}] '{name}' died - dodge escalation advanced to run {_runIndex} (failure chance reset to {CurrentFailureChance():P0}).", this);
    }

    /// <summary>
    /// Current probabilistic dodge-failure chance for this run: baseFailureChance plus
    /// this run's per-success increment for every successful dodge so far, clamped to
    /// [0, 1]. Runs 1 and 2 escalate (7pp and 3pp per success respectively); run 3
    /// onward is always 0 - the roll is disabled, though the jump itself can still fail
    /// physically.
    /// </summary>
    private float CurrentFailureChance()
    {
        if (_runIndex >= 3)
        {
            return 0f;
        }

        float increment = _runIndex == 1 ? run1FailureIncrement : run2FailureIncrement;
        return Mathf.Clamp01(baseFailureChance + increment * _successfulDodgeCount);
    }

    private void FixedUpdate()
    {
        if (motor == null || playback == null)
        {
            return;
        }

        if (_progression != null && _progression.IsDying)
        {
            Cancel();
            return;
        }

        if (_cooldownRemaining > 0f)
        {
            _cooldownRemaining -= Time.fixedDeltaTime;
        }

        if (_reacting)
        {
            TickReaction();
            return;
        }

        // Suspicious no longer blocks this - NpcCommandPlayback.BeginExternalControl()
        // lifts the Suspicious freeze for the duration of the jump and re-applies it
        // afterward (from wherever the jump ended up) if still relevant by then. Still
        // skip while already mid-reaction (handled above) or not grounded.
        if (_cooldownRemaining > 0f || !motor.IsGrounded)
        {
            return;
        }

        if (!TryFindClosingMonster(out MonsterPatrolController threat, out float timeToCollision))
        {
            return;
        }

        float failureChance = CurrentFailureChance();
        if (Random.value < failureChance)
        {
            // Rolled to ignore this specific encounter entirely - starts the same
            // cooldown a real reaction would, so it isn't just re-rolled again a tick
            // later against the same still-closing monster. Does not count as a
            // successful dodge and does not advance the escalation.
            Debug.Log($"[{nameof(NpcMonsterJumpReaction)}] Dodge FAILED (run {_runIndex}, chance {failureChance:P0}, successes {_successfulDodgeCount}) vs '{threat.name}', time-to-collision={timeToCollision:F2}s.", this);
            _cooldownRemaining = reactionCooldown;
            return;
        }

        // Passed the probabilistic dodge check and is entering the reaction path - this
        // is what counts as a "successful dodge" for the escalation, regardless of
        // whether the jump itself ends up physically avoiding the monster.
        _successfulDodgeCount++;

        Debug.Log($"[{nameof(NpcMonsterJumpReaction)}] Dodge SUCCEEDED (run {_runIndex}, chance was {failureChance:P0}, successes now {_successfulDodgeCount}) vs '{threat.name}', time-to-collision={timeToCollision:F2}s.", this);
        BeginReaction();
    }

    /// <summary>Ground-check position when available (feet-level), falling back to the transform's own position otherwise.</summary>
    private Vector3 DetectionOrigin => motor != null && motor.GroundCheck != null ? motor.GroundCheck.position : transform.position;

    /// <summary>
    /// Searches detectionRadius for the most urgent Monster (soonest time-to-collision
    /// within reactionLeadTime). Closing speed is derived from actual current
    /// velocities on both sides, not just patrol direction, so a monster braking into
    /// a turn or an NPC still ramping up from a standstill doesn't get treated as
    /// approaching at full speed.
    /// </summary>
    private bool TryFindClosingMonster(out MonsterPatrolController threat, out float timeToCollision)
    {
        threat = null;
        timeToCollision = float.PositiveInfinity;

        Vector3 origin = DetectionOrigin;
        float npcVelocityX = motor.Velocity.x;

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, detectionRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            MonsterPatrolController monster = hits[i].GetComponentInParent<MonsterPatrolController>();
            if (monster == null)
            {
                continue;
            }

            CharacterMotor2D monsterMotor = monster.GetComponent<CharacterMotor2D>();
            if (monsterMotor == null)
            {
                continue;
            }

            float dx = monster.transform.position.x - origin.x;
            if (Mathf.Approximately(dx, 0f))
            {
                // Already overlapping - Monster's own side-contact kill handles this;
                // there is no meaningful "closing speed" for zero distance.
                continue;
            }

            // +1 if the monster is to the right, -1 if to the left. Projecting both
            // velocities onto this axis turns "are they closing?" into a single signed
            // number: positive means the gap is shrinking, however fast.
            float towardMonster = Mathf.Sign(dx);
            float closingSpeed = (npcVelocityX - monsterMotor.Velocity.x) * towardMonster;
            if (closingSpeed <= 0f)
            {
                continue;
            }

            float estimatedTime = Mathf.Abs(dx) / closingSpeed;
            if (estimatedTime > reactionLeadTime)
            {
                continue;
            }

            if (estimatedTime < timeToCollision)
            {
                timeToCollision = estimatedTime;
                threat = monster;
            }
        }

        return threat != null;
    }

    private void BeginReaction()
    {
        _reacting = true;
        _hasLeftGround = false;

        // Suppress the recording for the whole reaction so it cannot issue a
        // conflicting SetMoveInput/RequestJump/SetJumpHeld call while this is in
        // control. Physics keeps simulating (unlike Pause()), so the jump itself can
        // actually happen.
        playback.BeginExternalControl();

        // Zero horizontal input so this is a jump in place, not a jump that also
        // carries whatever the recording's last move input happened to be.
        motor.SetMoveInput(0f);
        motor.RequestJump();
    }

    private void TickReaction()
    {
        // Hold still for the whole arc; nothing else is driving moveInput while
        // external control is active.
        motor.SetMoveInput(0f);

        if (!_hasLeftGround)
        {
            // IsGrounded can stay true for a tick or two after RequestJump before the
            // rigidbody actually leaves the ground - wait for an actual airborne tick
            // before treating a later "grounded" reading as having landed.
            if (!motor.IsGrounded)
            {
                _hasLeftGround = true;
            }
            return;
        }

        if (motor.IsGrounded)
        {
            EndReaction();
        }
    }

    private void EndReaction()
    {
        // The jump buffer/coyote timers are already clean by this point - consumed
        // the instant the jump fired, and coyote is continuously self-refreshed by
        // CharacterMotor2D while grounded regardless of what happens here. jumpHeld
        // is the one flag that does not self-clear from physics alone, so it is the
        // only thing explicitly reset - deliberately not ResetTransientState(), which
        // would also zero the horizontal accel run-up and disturb replay fidelity
        // right as the recording resumes.
        motor.SetJumpHeld(false);
        playback.EndExternalControl();

        _reacting = false;
        _cooldownRemaining = reactionCooldown;
    }

    // Not gated on selection (unlike Monster's wall/ledge gizmos) so the detection
    // range can be eyeballed while placing monsters relative to an NPC without having
    // to click the NPC every time - showDetectionGizmos is the actual on/off switch.
    private void OnDrawGizmos()
    {
        if (!showDetectionGizmos)
        {
            return;
        }

        if (motor == null)
        {
            motor = GetComponent<CharacterMotor2D>();
        }

        Vector3 origin = DetectionOrigin;

        Gizmos.color = detectionRadiusColor;
        Gizmos.DrawWireSphere(origin, detectionRadius);

        Gizmos.color = worstCaseRadiusColor;
        Gizmos.DrawWireSphere(origin, ComputeWorstCaseRadius());
    }

    /// <summary>
    /// The trigger range is no longer a fixed distance - it depends on live closing
    /// speed - so this draws the largest distance it could possibly fire from:
    /// (this NPC's own move speed + the fastest Monster currently in the scene) *
    /// reactionLeadTime, i.e. two things running straight at each other as fast as
    /// they're able to. Actual trigger range at any given moment is usually smaller.
    /// </summary>
    private float ComputeWorstCaseRadius()
    {
        float npcSpeed = motor != null ? motor.MoveSpeed : 0f;

        float maxMonsterSpeed = 0f;
        MonsterPatrolController[] monsters = FindObjectsByType<MonsterPatrolController>();
        for (int i = 0; i < monsters.Length; i++)
        {
            CharacterMotor2D monsterMotor = monsters[i] != null ? monsters[i].GetComponent<CharacterMotor2D>() : null;
            if (monsterMotor != null)
            {
                maxMonsterSpeed = Mathf.Max(maxMonsterSpeed, monsterMotor.MoveSpeed);
            }
        }

        return (npcSpeed + maxMonsterSpeed) * reactionLeadTime;
    }
}
