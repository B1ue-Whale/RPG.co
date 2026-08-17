using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Checkpoint-aware workflow around PlayerCommandRecorder: prepares the player at
/// startCheckpoint's anchor, records normally, and auto-stops/saves the moment the
/// player validly reaches endCheckpoint. Saving to an asset is editor-only - this spike
/// has no runtime recording-save format.
/// </summary>
// PlayerCommandRecorder and CharacterMotor2D both run at the default order 0, so their
// relative order within a tick is unspecified. +50 guarantees this component's
// FixedUpdate always runs after both, so HasArrived() below reads that tick's
// already-fresh IsGrounded (not a stale value from before CharacterMotor2D ran) and
// StopRecording() never fires before PlayerCommandRecorder has appended that tick's
// command - otherwise the saved recording could be truncated one tick short of the
// player's actual landing.
[DefaultExecutionOrder(50)]
public class CheckpointRecordingController : MonoBehaviour
{
    [Header("Player refs")]
    [SerializeField] private PlayerCommandRecorder recorder;
    [SerializeField] private Rigidbody2D playerBody;

    [Header("Checkpoints")]
    [SerializeField] private ProgressCheckpoint startCheckpoint;
    [SerializeField] private ProgressCheckpoint endCheckpoint;

    [Header("Save location (Editor only)")]
    [SerializeField] private string saveFolder = "Assets/Recordings";

    public bool IsRecording => recorder != null && recorder.IsRecording;
    public PlayerCommandRecorder Recorder => recorder;
    public ProgressCheckpoint StartCheckpoint => startCheckpoint;
    public ProgressCheckpoint EndCheckpoint => endCheckpoint;
    public int RecordedTickCount => recorder != null ? recorder.Commands.Count : 0;
    public float RecordedSeconds => RecordedTickCount * Time.fixedDeltaTime;

    /// <summary>Raised after a recording is successfully saved as an asset.</summary>
    public event System.Action<NpcRecording> RecordingSaved;

    public void SetCheckpoints(ProgressCheckpoint start, ProgressCheckpoint end)
    {
        startCheckpoint = start;
        endCheckpoint = end;
    }

    /// <summary>Teleports the player to startCheckpoint's anchor and clears stale velocity/motor state.</summary>
    public void PrepareAtStart()
    {
        if (startCheckpoint == null || recorder == null)
        {
            Debug.LogWarning($"[{nameof(CheckpointRecordingController)}] Missing start checkpoint or recorder.");
            return;
        }

        startCheckpoint.SnapTo(playerBody, recorder.Motor);
    }

    public void BeginRecording()
    {
        if (startCheckpoint == null || endCheckpoint == null || recorder == null)
        {
            Debug.LogWarning($"[{nameof(CheckpointRecordingController)}] Assign recorder, start checkpoint and end checkpoint before recording.");
            return;
        }

        PrepareAtStart();
        recorder.StartRecording();
    }

    private void FixedUpdate()
    {
        if (!IsRecording || endCheckpoint == null)
        {
            return;
        }

        // Guard against a same-tick false positive if start and end happen to overlap.
        if (recorder.Commands.Count == 0)
        {
            return;
        }

        if (endCheckpoint.HasArrived(playerBody, recorder.Motor))
        {
            CompleteRecording();
        }
    }

    private void CompleteRecording()
    {
        recorder.StopRecording();
        SaveRecording(recorder.Commands);
    }

    private void SaveRecording(IReadOnlyList<MotorCommand> commands)
    {
#if UNITY_EDITOR
        if (!AssetDatabase.IsValidFolder(saveFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Recordings");
        }

        NpcRecording asset = ScriptableObject.CreateInstance<NpcRecording>();
        asset.Initialize(startCheckpoint.Id, endCheckpoint.Id, commands);

        string baseName = $"Recording_{startCheckpoint.Id}_to_{endCheckpoint.Id}";
        string path = AssetDatabase.GenerateUniqueAssetPath($"{saveFolder}/{baseName}.asset");
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();

        Debug.Log($"[{nameof(CheckpointRecordingController)}] Saved recording '{path}' ({commands.Count} ticks, {commands.Count * Time.fixedDeltaTime:F2}s).");
        RecordingSaved?.Invoke(asset);
#else
        Debug.LogWarning($"[{nameof(CheckpointRecordingController)}] Recording save is editor-only; {commands.Count} ticks were captured but not saved.");
#endif
    }
}
