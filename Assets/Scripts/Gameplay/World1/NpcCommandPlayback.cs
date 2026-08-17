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
// SetMoveInput/RequestJump for tick N are applied before CharacterMotor2D consumes
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

    public bool IsPlaying { get; private set; }
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
    }

    public void SetRecording(IReadOnlyList<MotorCommand> commands)
    {
        _commands = commands ?? System.Array.Empty<MotorCommand>();
    }

    public void Play()
    {
        _tickIndex = 0;
        IsPlaying = _commands.Count > 0;
        if (!IsPlaying)
        {
            Debug.LogWarning($"[{nameof(NpcCommandPlayback)}] No recording to play.");
        }
    }

    public void Stop()
    {
        IsPlaying = false;
        // Otherwise the NPC keeps drifting on whatever moveInput was last applied.
        motor?.SetMoveInput(0f);
    }

    /// <summary>
    /// Snaps the NPC back to startPoint with zero velocity and rewinds playback to
    /// tick 0. Does not reset CharacterMotor2D's internal coyote/jump-buffer timers
    /// directly - there's no accessor for them, and it wasn't necessary to add one for
    /// this spike, since they settle within a tick or two once grounded again.
    /// </summary>
    public void ResetToStart()
    {
        Stop();
        _tickIndex = 0;

        if (body != null && startPoint != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = startPoint.position;
            body.rotation = 0f;
        }
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
        if (!IsPlaying || motor == null)
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
            motor.RequestJump();
        }
        if (cmd.interactRequested)
        {
            interactionAgent?.TryInteract();
        }

        _tickIndex++;
    }
}
