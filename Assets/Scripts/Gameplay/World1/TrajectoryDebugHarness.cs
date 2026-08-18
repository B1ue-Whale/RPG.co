using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// TEMPORARY debug harness for the trajectory-divergence investigation. Records a
/// segment via CheckpointRecordingController as usual, then immediately replays the
/// in-memory commands on the NPC (bypassing the saved-asset / NpcProgressionController
/// chain path) while TrajectoryDebugComparator watches for the first tick where the
/// NPC's actual state diverges from the recorded reference. Delete once done.
/// </summary>
public class TrajectoryDebugHarness : MonoBehaviour
{
    [SerializeField] private CheckpointRecordingController recordingController;
    [SerializeField] private TrajectoryDebugRecorder trajectoryRecorder;
    [SerializeField] private NpcCommandPlayback npcPlayback;
    [SerializeField] private TrajectoryDebugComparator comparator;

    [Header("Keybinds")]
    [SerializeField] private Key prepareKey = Key.Digit1;
    [SerializeField] private Key beginRecordingKey = Key.Digit2;
    [SerializeField] private Key replayAndCompareKey = Key.Digit3;

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[prepareKey].wasPressedThisFrame)
        {
            recordingController?.PrepareAtStart();
        }

        if (Keyboard.current[beginRecordingKey].wasPressedThisFrame)
        {
            recordingController?.BeginRecording();
        }

        if (Keyboard.current[replayAndCompareKey].wasPressedThisFrame)
        {
            ReplayAndCompare();
        }
    }

    private void ReplayAndCompare()
    {
        if (recordingController == null || trajectoryRecorder == null || npcPlayback == null || comparator == null)
        {
            Debug.LogWarning($"[{nameof(TrajectoryDebugHarness)}] Missing references.");
            return;
        }

        if (recordingController.StartCheckpoint == null)
        {
            Debug.LogWarning($"[{nameof(TrajectoryDebugHarness)}] No start checkpoint assigned on the recording controller.");
            return;
        }

        var commands = recordingController.Recorder != null ? recordingController.Recorder.Commands : System.Array.Empty<MotorCommand>();

        npcPlayback.PrepareAtCheckpoint(recordingController.StartCheckpoint);
        comparator.BeginComparison(trajectoryRecorder.States, commands);
        npcPlayback.SetRecording(commands);
        npcPlayback.Play();
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(610, 10, 320, 130), GUI.skin.box);
        GUILayout.Label("Trajectory Divergence Debug");
        GUILayout.Label($"[{prepareKey}] Prepare   [{beginRecordingKey}] Record   [{replayAndCompareKey}] Replay+Compare");
        if (recordingController != null)
        {
            GUILayout.Label(recordingController.IsRecording ? $"Recording: ON ({recordingController.RecordedTickCount} ticks)" : "Recording: off");
        }
        GUILayout.EndArea();
    }
}
