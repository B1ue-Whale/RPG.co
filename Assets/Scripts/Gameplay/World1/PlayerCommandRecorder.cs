using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Captures real player input as physics-tick-granularity <see cref="MotorCommand"/>s,
/// for replay through another CharacterMotor2D. In-memory only - no asset/save format.
/// </summary>
public class PlayerCommandRecorder : MonoBehaviour
{
    [Tooltip("The player's motor. Move input is read directly from it each tick, so the recording always matches exactly what the motor is about to consume.")]
    [SerializeField] private CharacterMotor2D motor;
    [Tooltip("The player's interaction agent. Interact presses are only recorded if this is assigned.")]
    [SerializeField] private InteractionAgent interactionAgent;

    private readonly List<MotorCommand> _commands = new List<MotorCommand>();
    private bool _jumpRequestedSinceLastTick;
    private bool _interactRequestedSinceLastTick;

    public bool IsRecording { get; private set; }
    public IReadOnlyList<MotorCommand> Commands => _commands;
    public CharacterMotor2D Motor => motor;

    private void OnEnable()
    {
        if (motor != null)
        {
            motor.JumpRequested += OnJumpRequested;
        }
        if (interactionAgent != null)
        {
            interactionAgent.Interacted += OnInteracted;
        }
    }

    private void OnDisable()
    {
        if (motor != null)
        {
            motor.JumpRequested -= OnJumpRequested;
        }
        if (interactionAgent != null)
        {
            interactionAgent.Interacted -= OnInteracted;
        }
    }

    // These latch to true the instant RequestJump()/TryInteract() are called (during
    // Update-phase input handling, not FixedUpdate), and are only ever read/cleared by
    // this component's own FixedUpdate below. Because no other component touches them,
    // capturing "was jump/interact requested this tick" needs no ordering guarantee
    // relative to CharacterMotor2D.FixedUpdate - see FixedUpdate() comment.
    private void OnJumpRequested() => _jumpRequestedSinceLastTick = true;
    private void OnInteracted() => _interactRequestedSinceLastTick = true;

    public void StartRecording()
    {
        _commands.Clear();
        _jumpRequestedSinceLastTick = false;
        _interactRequestedSinceLastTick = false;
        IsRecording = true;
        Debug.Log($"[{nameof(PlayerCommandRecorder)}] Recording started.");
    }

    public void StopRecording()
    {
        IsRecording = false;
        Debug.Log($"[{nameof(PlayerCommandRecorder)}] Recording stopped. {_commands.Count} ticks captured.");
    }

    // No execution-order dependency on CharacterMotor2D is needed here:
    // - moveInput/jumpHeld are read from CharacterMotor2D fields the motor never
    //   mutates inside its own FixedUpdate, so this is safe to sample regardless of
    //   whether this FixedUpdate runs before or after the motor's this tick.
    // - jump/interact presses are read from latches this component owns exclusively
    //   (see above), not from the motor's internal buffer-timer state, so there is
    //   nothing for CharacterMotor2D.FixedUpdate to race with or clear out from under us.
    private void FixedUpdate()
    {
        if (!IsRecording || motor == null)
        {
            return;
        }

        _commands.Add(new MotorCommand
        {
            moveInput = motor.PendingMoveInput,
            jumpRequested = _jumpRequestedSinceLastTick,
            jumpHeld = motor.JumpHeld,
            interactRequested = _interactRequestedSinceLastTick
        });

        _jumpRequestedSinceLastTick = false;
        _interactRequestedSinceLastTick = false;
    }
}
