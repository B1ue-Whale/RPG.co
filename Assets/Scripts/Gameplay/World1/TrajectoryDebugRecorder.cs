using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TEMPORARY diagnostic - not part of the persisted recording format. Captures a
/// MotorStateSample every tick alongside PlayerCommandRecorder's MotorCommand stream
/// (same tick indices, so States[i] lines up with PlayerCommandRecorder.Commands[i]),
/// so a replay can be compared tick-for-tick against what actually happened during the
/// original recording. Delete once the trajectory-divergence investigation is done.
/// </summary>
// Runs after CharacterMotor2D (order 0), so velocity/grounded reflect this tick's
// command having already been applied by CharacterMotor2D.FixedUpdate - the same
// convention TrajectoryDebugComparator uses on the NPC side, so samples on both sides
// are directly comparable index-for-index.
[DefaultExecutionOrder(60)]
public class TrajectoryDebugRecorder : MonoBehaviour
{
    [SerializeField] private PlayerCommandRecorder recorder;
    [SerializeField] private CharacterMotor2D motor;

    private readonly List<MotorStateSample> _states = new List<MotorStateSample>();
    private bool _wasRecording;

    public IReadOnlyList<MotorStateSample> States => _states;

    private void FixedUpdate()
    {
        if (recorder == null || motor == null)
        {
            return;
        }

        bool isRecording = recorder.IsRecording;
        if (isRecording && !_wasRecording)
        {
            _states.Clear();
        }
        _wasRecording = isRecording;

        if (!isRecording)
        {
            return;
        }

        _states.Add(new MotorStateSample
        {
            position = motor.transform.position,
            velocity = motor.Velocity,
            grounded = motor.IsGrounded
        });
    }
}
