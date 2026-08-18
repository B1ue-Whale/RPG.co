using UnityEngine;

/// <summary>
/// TEMPORARY diagnostic - not part of the recording/playback system itself. Watches
/// PlayerCommandRecorder.IsRecording and NpcCommandPlayback.IsPlaying for start/stop
/// edges and logs every timing metric requested for the tick-cadence investigation:
/// command count, simulated duration, Time.time/Time.fixedTime/Time.realtimeSinceStartup
/// elapsed, and FixedUpdate call count. Delete once the investigation is done.
/// </summary>
// After both PlayerCommandRecorder/CharacterMotor2D (order 0) and NpcCommandPlayback
// (order -100), so a stop-edge detected this tick already reflects that tick's final
// Commands.Count / ConsumedTickCount - not one tick stale.
[DefaultExecutionOrder(70)]
public class RecordingTimingDiagnostics : MonoBehaviour
{
    [SerializeField] private PlayerCommandRecorder recorder;
    [SerializeField] private NpcCommandPlayback playback;

    private bool _wasRecording;
    private int _recordFixedUpdateCalls;
    private float _recordStartTime;
    private float _recordStartFixedTime;
    private float _recordStartReal;

    private bool _wasPlaying;
    private int _playbackFixedUpdateCalls;
    private float _playbackStartTime;
    private float _playbackStartFixedTime;
    private float _playbackStartReal;

    private void FixedUpdate()
    {
        TrackRecording();
        TrackPlayback();
    }

    private void TrackRecording()
    {
        if (recorder == null)
        {
            return;
        }

        bool isRecording = recorder.IsRecording;

        if (isRecording && !_wasRecording)
        {
            _recordFixedUpdateCalls = 0;
            _recordStartTime = Time.time;
            _recordStartFixedTime = Time.fixedTime;
            _recordStartReal = Time.realtimeSinceStartup;
        }

        if (isRecording)
        {
            _recordFixedUpdateCalls++;
        }

        if (!isRecording && _wasRecording)
        {
            int ticks = recorder.Commands.Count;
            float simDuration = ticks * Time.fixedDeltaTime;
            Debug.Log(
                $"[{nameof(RecordingTimingDiagnostics)}] RECORD SUMMARY: commandCount={ticks} "
                + $"simDuration(ticks*fixedDeltaTime)={simDuration:F3}s "
                + $"Time.time elapsed={Time.time - _recordStartTime:F3}s "
                + $"Time.fixedTime elapsed={Time.fixedTime - _recordStartFixedTime:F3}s "
                + $"Time.realtimeSinceStartup elapsed={Time.realtimeSinceStartup - _recordStartReal:F3}s "
                + $"FixedUpdate calls while recording={_recordFixedUpdateCalls}");
        }

        _wasRecording = isRecording;
    }

    private void TrackPlayback()
    {
        if (playback == null)
        {
            return;
        }

        bool isPlaying = playback.IsPlaying;

        if (isPlaying && !_wasPlaying)
        {
            _playbackFixedUpdateCalls = 0;
            _playbackStartTime = Time.time;
            _playbackStartFixedTime = Time.fixedTime;
            _playbackStartReal = Time.realtimeSinceStartup;
        }

        if (isPlaying)
        {
            _playbackFixedUpdateCalls++;
        }

        if (!isPlaying && _wasPlaying)
        {
            Debug.Log(
                $"[{nameof(RecordingTimingDiagnostics)}] PLAYBACK SUMMARY: "
                + $"commandsConsumed={playback.ConsumedTickCount} "
                + $"FixedUpdate calls while playing={_playbackFixedUpdateCalls} "
                + $"Time.time elapsed={Time.time - _playbackStartTime:F3}s "
                + $"Time.fixedTime elapsed={Time.fixedTime - _playbackStartFixedTime:F3}s "
                + $"Time.realtimeSinceStartup elapsed={Time.realtimeSinceStartup - _playbackStartReal:F3}s");
        }

        _wasPlaying = isPlaying;
    }
}
