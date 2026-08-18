using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Play-mode debug UI for the checkpoint recording workflow - not production tooling.
/// Mirrors RecordingSpikeHarness's keybind pattern but drives
/// CheckpointRecordingController, which auto-stops/saves on arrival at the end
/// checkpoint instead of needing a manual stop key.
/// </summary>
public class CheckpointRecordingHarness : MonoBehaviour
{
    [SerializeField] private CheckpointRecordingController controller;

    [Header("Keybinds")]
    [SerializeField] private Key prepareKey = Key.Digit1;
    [SerializeField] private Key beginRecordingKey = Key.Digit2;

    private void Update()
    {
        if (Keyboard.current == null || controller == null)
        {
            return;
        }

        if (Keyboard.current[prepareKey].wasPressedThisFrame)
        {
            controller.PrepareAtStart();
        }

        if (Keyboard.current[beginRecordingKey].wasPressedThisFrame)
        {
            controller.BeginRecording();
        }
    }

    private void OnGUI()
    {
        if (controller == null)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(300, 10, 300, 150), GUI.skin.box);
        GUILayout.Label("Checkpoint Recording");
        GUILayout.Label($"Start: {(controller.StartCheckpoint != null ? controller.StartCheckpoint.Id : "-")}   End: {(controller.EndCheckpoint != null ? controller.EndCheckpoint.Id : "-")}");
        GUILayout.Label($"[{prepareKey}] Prepare at Start   [{beginRecordingKey}] Begin Recording");

        if (GUILayout.Button("Prepare at Start"))
        {
            controller.PrepareAtStart();
        }
        if (GUILayout.Button("Begin Recording"))
        {
            controller.BeginRecording();
        }

        GUILayout.Label(controller.IsRecording
            ? $"Recording: ON ({controller.RecordedTickCount} ticks, {controller.RecordedSeconds:F2}s) - walk to End to auto-save"
            : "Recording: off");
        GUILayout.EndArea();
    }
}
