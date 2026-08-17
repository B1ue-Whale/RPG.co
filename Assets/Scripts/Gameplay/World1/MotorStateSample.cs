using UnityEngine;

/// <summary>
/// TEMPORARY diagnostic-only per-tick snapshot of resulting physical state (not
/// intention, unlike MotorCommand). Used by TrajectoryDebugRecorder/
/// TrajectoryDebugComparator to find where a replay's trajectory first diverges from
/// the run it was recorded from. Never persisted into NpcRecording.
/// </summary>
[System.Serializable]
public struct MotorStateSample
{
    public Vector2 position;
    public Vector2 velocity;
    public bool grounded;
}
