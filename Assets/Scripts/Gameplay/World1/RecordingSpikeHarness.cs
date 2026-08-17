using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Disposable Play-mode debug UI for the record/replay spike - not production tooling.
/// Drives PlayerCommandRecorder/NpcCommandPlayback manually for the A-to-B test.
/// <para>
/// Has both on-screen buttons and keybinds. A mouse click on a button is
/// indistinguishable, from the Input System's point of view, from a click on the
/// player's own "Attack" binding (<c>&lt;Mouse&gt;/leftButton</c>) - OnGUI does not
/// suppress it - but PlayerController.OnAttack currently has its Attack() call
/// disabled, so this is harmless for now. Re-check this if Attack is re-enabled.
/// </para>
/// </summary>
public class RecordingSpikeHarness : MonoBehaviour
{
    [SerializeField] private PlayerCommandRecorder recorder;
    [SerializeField] private NpcCommandPlayback playback;

    // Function keys (F5/F6/...) are avoided here - they're commonly bound to
    // "Start Debugging"/build shortcuts in Visual Studio/VS Code, and can get
    // intercepted by the IDE instead of Unity if it has focus.
    [Header("Keybinds")]
    [SerializeField] private Key startRecordingKey = Key.Digit1;
    [SerializeField] private Key stopRecordingKey = Key.Digit2;
    [SerializeField] private Key resetNpcKey = Key.Digit3;
    [SerializeField] private Key replayKey = Key.Digit4;

    [Header("No physical collision between these two")]
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private Collider2D npcCollider;

    private void Awake()
    {
        if (playerCollider != null && npcCollider != null)
        {
            Physics2D.IgnoreCollision(playerCollider, npcCollider, true);
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[startRecordingKey].wasPressedThisFrame)
        {
            StartRecording();
        }

        if (Keyboard.current[stopRecordingKey].wasPressedThisFrame)
        {
            StopRecording();
        }

        if (Keyboard.current[resetNpcKey].wasPressedThisFrame)
        {
            ResetNpc();
        }

        if (Keyboard.current[replayKey].wasPressedThisFrame)
        {
            Replay();
        }
    }

    private void StartRecording()
    {
        recorder?.StartRecording();
    }

    private void StopRecording()
    {
        recorder?.StopRecording();
    }

    private void ResetNpc()
    {
        playback?.ResetToStart();
    }

    private void Replay()
    {
        if (playback == null || recorder == null)
        {
            return;
        }
        playback.SetRecording(recorder.Commands);
        playback.Play();
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 280, 280), GUI.skin.box);
        GUILayout.Label("NPC Replay Spike");
        GUILayout.Label($"[{startRecordingKey}] Start   [{stopRecordingKey}] Stop Recording");
        GUILayout.Label($"[{resetNpcKey}] Reset NPC   [{replayKey}] Start Playback");

        if (GUILayout.Button("Start Recording"))
        {
            StartRecording();
        }
        if (GUILayout.Button("Stop Recording"))
        {
            StopRecording();
        }
        if (GUILayout.Button("Reset NPC"))
        {
            ResetNpc();
        }
        if (GUILayout.Button("Start Playback"))
        {
            Replay();
        }

        if (recorder != null)
        {
            float seconds = recorder.Commands.Count * Time.fixedDeltaTime;
            GUILayout.Label($"Recording: {(recorder.IsRecording ? "ON" : "off")} ({recorder.Commands.Count} ticks, {seconds:F2}s)");
        }
        if (playback != null)
        {
            GUILayout.Label($"Playback: {(playback.IsPlaying ? "playing" : "idle")}");
        }

        GUILayout.EndArea();
    }
}
