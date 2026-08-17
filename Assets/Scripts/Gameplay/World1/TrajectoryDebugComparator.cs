using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TEMPORARY diagnostic - compares the NPC's actual per-tick state against a reference
/// trajectory captured by TrajectoryDebugRecorder during the original recording, and
/// logs the FIRST tick where they diverge beyond tolerance, plus a compact line at
/// every recorded jump tick for context. Delete once the trajectory-divergence
/// investigation is done.
/// </summary>
// Same execution order as TrajectoryDebugRecorder (after CharacterMotor2D), so both
// sides sample state at the same point in the tick and are directly comparable.
[DefaultExecutionOrder(60)]
public class TrajectoryDebugComparator : MonoBehaviour
{
    [SerializeField] private NpcCommandPlayback playback;
    [Tooltip("Position error (units) beyond which a tick counts as a divergence.")]
    [SerializeField] private float positionErrorThreshold = 0.15f;

    private IReadOnlyList<MotorStateSample> _reference = Array.Empty<MotorStateSample>();
    private IReadOnlyList<MotorCommand> _commands = Array.Empty<MotorCommand>();
    private int _tickIndex;
    private bool _armed;
    private bool _divergenceLogged;

    /// <summary>Arms the comparator. Call this immediately before playback.Play().</summary>
    public void BeginComparison(IReadOnlyList<MotorStateSample> reference, IReadOnlyList<MotorCommand> commands)
    {
        _reference = reference ?? Array.Empty<MotorStateSample>();
        _commands = commands ?? Array.Empty<MotorCommand>();
        _tickIndex = 0;
        _armed = true;
        _divergenceLogged = false;
        Debug.Log($"[{nameof(TrajectoryDebugComparator)}] Armed - comparing against {_reference.Count} reference ticks.");
    }

    private void FixedUpdate()
    {
        if (!_armed || playback == null || !playback.IsPlaying)
        {
            return;
        }

        if (_tickIndex >= _reference.Count)
        {
            return;
        }

        MotorStateSample reference = _reference[_tickIndex];
        MotorStateSample actual = new MotorStateSample
        {
            position = playback.Motor.transform.position,
            velocity = playback.Motor.Velocity,
            grounded = playback.Motor.IsGrounded
        };

        float positionError = Vector2.Distance(reference.position, actual.position);
        bool jumpTick = _tickIndex < _commands.Count && _commands[_tickIndex].jumpRequested;

        if (jumpTick)
        {
            Debug.Log($"[{nameof(TrajectoryDebugComparator)}] tick {_tickIndex} (jump) error={positionError:F3} "
                + $"ref(pos={reference.position}, vel={reference.velocity}, grounded={reference.grounded}) "
                + $"actual(pos={actual.position}, vel={actual.velocity}, grounded={actual.grounded})");
        }

        if (!_divergenceLogged && positionError > positionErrorThreshold)
        {
            _divergenceLogged = true;
            Debug.LogWarning($"[{nameof(TrajectoryDebugComparator)}] FIRST DIVERGENCE at tick {_tickIndex}/{_reference.Count} "
                + $"error={positionError:F3} "
                + $"ref(pos={reference.position}, vel={reference.velocity}, grounded={reference.grounded}) "
                + $"actual(pos={actual.position}, vel={actual.velocity}, grounded={actual.grounded})");
        }

        _tickIndex++;

        if (_tickIndex >= _reference.Count)
        {
            Debug.Log(_divergenceLogged
                ? $"[{nameof(TrajectoryDebugComparator)}] Comparison finished with a divergence flagged above."
                : $"[{nameof(TrajectoryDebugComparator)}] Comparison finished - no divergence exceeded {positionErrorThreshold} units.");
        }
    }
}
