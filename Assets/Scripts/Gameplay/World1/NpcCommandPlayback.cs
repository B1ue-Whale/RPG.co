using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Feeds a recorded MotorCommand stream into an NPC's CharacterMotor2D/InteractionAgent
/// at the same physics-tick cadence it was recorded at. Depends only on
/// CharacterMotor2D and InteractionAgent - never on PlayerController or any input
/// polling - so it drives the NPC entirely independently of the player.
/// </summary>
// Default execution order is 0, which is what CharacterMotor2D.FixedUpdate runs at.
// -100 guarantees this component's FixedUpdate runs first every tick, so
// SetMoveInput/RequestJump/SetJumpHeld for tick N are applied before CharacterMotor2D consumes
// tick N - not one tick late. This is Unity's documented, code-level ordering
// mechanism (equivalent to the Project Settings > Script Execution Order list, but
// declared in source instead of a hidden project asset), not incidental component
// ordering.
[DefaultExecutionOrder(-100)]
public class NpcCommandPlayback : MonoBehaviour
{
    [SerializeField] private CharacterMotor2D motor;
    [SerializeField] private InteractionAgent interactionAgent;
    [SerializeField] private Rigidbody2D body;
    [Tooltip("Pose the NPC is snapped back to by ResetToStart().")]
    [SerializeField] private Transform startPoint;

    private IReadOnlyList<MotorCommand> _commands = System.Array.Empty<MotorCommand>();
    private int _tickIndex;
    // Old recordings predate jumpHeld. Those assets deserialize the new field as
    // false on every tick; applying that would cut every jump. Only replay hold
    // when the stream actually recorded a hold at least once.
    private bool _replayJumpHeld;
    private bool _bodyFrozen;
    private Vector2 _pausedVelocity;
    private RigidbodyType2D _pausedBodyType;

    public bool IsPlaying { get; private set; }
    /// <summary>
    /// True while playback is paused mid-segment (e.g. NPC is Suspicious). Distinct
    /// from IsPlaying being false: a paused playback is still "in progress" - it just
    /// isn't consuming commands - so PlaybackCompleted does not fire and
    /// NpcProgressionController does not treat the segment as finished.
    /// </summary>
    public bool IsPaused { get; private set; }
    /// <summary>
    /// True while an external system (e.g. a monster-avoidance reaction) is driving
    /// the motor directly and command consumption must stay frozen. Unlike
    /// <see cref="IsPaused"/>/<see cref="Pause"/>, this does not freeze the rigidbody
    /// or touch CharacterMotor2D.SimulationPaused - physics keeps running normally so
    /// the external system's own motor calls (e.g. a jump) can actually execute.
    /// Resumes from the exact same tick once cleared, same as a Pause/Resume cycle.
    /// </summary>
    public bool IsExternallyControlled { get; private set; }
    public CharacterMotor2D Motor => motor;
    public Rigidbody2D Body => body;
    /// <summary>How many commands have been consumed so far (or in total, once stopped).</summary>
    public int ConsumedTickCount => _tickIndex;

    /// <summary>
    /// Raised when playback runs out of commands on its own (end of the recording).
    /// Not raised when <see cref="Stop"/> is called directly (e.g. an external cancel),
    /// so listeners can tell "segment finished" apart from "someone stopped it".
    /// </summary>
    public event System.Action PlaybackCompleted;

    private void Awake()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        SyncMotorSettingsFromPlayer();
    }

    public void SetRecording(IReadOnlyList<MotorCommand> commands)
    {
        _commands = commands ?? System.Array.Empty<MotorCommand>();
        _replayJumpHeld = false;
        for (int i = 0; i < _commands.Count; i++)
        {
            if (_commands[i].jumpHeld)
            {
                _replayJumpHeld = true;
                break;
            }
        }
    }

    public void Play()
    {
        _tickIndex = 0;
        IsPaused = false;
        IsPlaying = _commands.Count > 0;
        if (!IsPlaying)
        {
            Debug.LogWarning($"[{nameof(NpcCommandPlayback)}] No recording to play.");
        }
    }

    public void Stop()
    {
        IsPlaying = false;
        IsPaused = false;
        UnfreezeBody(restoreVelocity: false);
        // Otherwise the NPC keeps drifting on whatever moveInput was last applied,
        // and leftover jump-hold would turn the next jump into a full-height hop.
        ClearMotorIntent();
    }

    /// <summary>
    /// Pauses command consumption at the exact current _tickIndex. Freezes the body
    /// in place and leaves motor accel/jump state untouched so Resume() continues the
    /// recorded trajectory instead of restarting from a standstill. If an external
    /// controller currently has the motor (see <see cref="BeginExternalControl"/>),
    /// the freeze is deferred until it hands control back - freezing now would break
    /// whatever physics-dependent thing (e.g. a reaction jump) is in progress.
    /// </summary>
    public void Pause()
    {
        if (!IsPlaying || IsPaused)
        {
            return;
        }

        IsPaused = true;

        if (!IsExternallyControlled)
        {
            FreezeBody();
        }
    }

    /// <summary>
    /// Resumes consuming commands from the exact tick Pause() left off at, restoring
    /// the velocity that was frozen. No-op if not playing or not paused. If an
    /// external controller currently has the motor, the body is already unfrozen for
    /// it, so there is nothing to restore here - command consumption itself stays
    /// gated by IsExternallyControlled regardless.
    /// </summary>
    public void Resume()
    {
        if (!IsPlaying || !IsPaused)
        {
            return;
        }

        IsPaused = false;

        if (!IsExternallyControlled)
        {
            UnfreezeBody(restoreVelocity: true);
        }
    }

    /// <summary>
    /// Hands motor control to an external caller for the duration of some override
    /// behavior (e.g. a reaction jump): command consumption stops - no SetMoveInput,
    /// RequestJump, or SetJumpHeld calls from the recording. If currently Paused
    /// (Suspicious), the body's freeze is lifted too - a frozen, Kinematic body can't
    /// jump or fall, so physics has to actually run for the caller's own motor calls
    /// to do anything. Command consumption stays blocked regardless of IsPaused, via
    /// IsExternallyControlled alone, so this never resumes the recording early - it
    /// only lets physics run for whoever is driving the motor right now. The caller
    /// is responsible for driving the motor itself and calling
    /// <see cref="EndExternalControl"/> when done.
    /// </summary>
    public void BeginExternalControl()
    {
        IsExternallyControlled = true;

        if (IsPaused)
        {
            UnfreezeBody(restoreVelocity: true);
        }
    }

    /// <summary>
    /// Hands motor control back to whichever source should have it: the recorded
    /// command stream resumes from the exact tick it was suspended at, unless
    /// Suspicion is still active (IsPaused), in which case the freeze Pause()
    /// deferred is applied now instead - from wherever things ended up, not
    /// necessarily where Suspicious originally paused. If Suspicion exited on its own
    /// while control was external (Resume() already ran), there is nothing to
    /// re-freeze and this is a no-op beyond clearing the flag.
    /// </summary>
    public void EndExternalControl()
    {
        IsExternallyControlled = false;

        if (IsPaused)
        {
            FreezeBody();
        }
    }

    /// <summary>
    /// Snaps the NPC back to startPoint with zero velocity, clears motor transient
    /// state, and rewinds playback to tick 0.
    /// </summary>
    public void ResetToStart()
    {
        Stop();
        _tickIndex = 0;

        if (body != null && startPoint != null)
        {
            ProgressCheckpoint.TeleportRigidbody(body, startPoint.position);
        }

        motor?.ResetTransientState();
    }

    /// <summary>
    /// Checkpoint-aware equivalent of <see cref="ResetToStart"/>: snaps this NPC to
    /// checkpoint's anchor with zero velocity and clears the motor's stale jump/coyote
    /// state, and rewinds playback to tick 0.
    /// </summary>
    public void PrepareAtCheckpoint(ProgressCheckpoint checkpoint)
    {
        Stop();
        _tickIndex = 0;
        checkpoint?.SnapTo(body, motor);
    }

    private void FixedUpdate()
    {
        if (!IsPlaying || IsPaused || IsExternallyControlled || motor == null)
        {
            return;
        }

        if (_tickIndex >= _commands.Count)
        {
            Stop();
            PlaybackCompleted?.Invoke();
            return;
        }

        MotorCommand cmd = _commands[_tickIndex];
        motor.SetMoveInput(cmd.moveInput);
        if (cmd.jumpRequested)
        {
            motor.RequestJump(ignoreGroundedRequirement: true);
        }
        // After RequestJump so a same-tick tap (jumpRequested + jumpHeld false) can
        // still cut; RequestJump itself forces hold true.
        if (_replayJumpHeld)
        {
            motor.SetJumpHeld(cmd.jumpHeld);
        }
        if (cmd.interactRequested)
        {
            interactionAgent?.TryInteract();
        }

        _tickIndex++;
    }

    private void ClearMotorIntent()
    {
        motor?.SetMoveInput(0f);
        motor?.SetJumpHeld(false);
    }

    private void FreezeBody()
    {
        if (_bodyFrozen)
        {
            return;
        }

        if (motor != null)
        {
            motor.SimulationPaused = true;
        }

        if (body != null)
        {
            _pausedVelocity = body.linearVelocity;
            _pausedBodyType = body.bodyType;
            body.linearVelocity = Vector2.zero;
            body.bodyType = RigidbodyType2D.Kinematic;
        }

        _bodyFrozen = true;
    }

    private void UnfreezeBody(bool restoreVelocity)
    {
        if (motor != null)
        {
            motor.SimulationPaused = false;
        }

        if (_bodyFrozen && body != null)
        {
            body.bodyType = _pausedBodyType;
            body.linearVelocity = restoreVelocity ? _pausedVelocity : Vector2.zero;
        }

        _bodyFrozen = false;
    }

    private void SyncMotorSettingsFromPlayer()
    {
        if (motor == null)
        {
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            return;
        }

        CharacterMotor2D playerMotor = player.GetComponent<CharacterMotor2D>();
        if (playerMotor == null || playerMotor == motor)
        {
            return;
        }

        motor.CopyMovementSettingsFrom(playerMotor);
    }
}
