using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Drives an NPC through an ordered checkpoint chain (CP0 -> CP1 -> CP2 -> ...),
/// replaying a randomly chosen NpcRecording for each segment and verifying arrival
/// before advancing. Stage 1 only: no suspicious/noise/detection, pathfinding,
/// waypoint AI, or interrupt/resume.
/// </summary>
// NpcCommandPlayback runs its FixedUpdate at order -100 (fires PlaybackCompleted),
// CharacterMotor2D runs at the default order 0 (refreshes IsGrounded from the
// now-current position). +100 guarantees this component's FixedUpdate always runs
// after both within the same tick, so the arrival check below reads a grounded state
// that reflects the position the NPC actually ended at - not the stale, one-tick-old
// value CharacterMotor2D would otherwise still be holding.
[DefaultExecutionOrder(100)]
public class NpcProgressionController : MonoBehaviour
{
    [SerializeField] private NpcCommandPlayback playback;

    [Tooltip("Ordered checkpoint chain: CP0, CP1, CP2, ...")]
    [SerializeField] private List<ProgressCheckpoint> checkpointChain = new List<ProgressCheckpoint>();

    [Tooltip("All recordings available to this NPC. Each segment looks up recordings whose StartCheckpointId/EndCheckpointId match that segment's pair.")]
    [SerializeField] private List<NpcRecording> recordingLibrary = new List<NpcRecording>();

    [SerializeField] private bool autoStart = true;

    [Header("Arrival")]
    [Tooltip("After a segment's commands run out, how long to keep checking for a valid arrival before logging a diagnostic warning. Covers the tick or two it takes CharacterMotor2D's IsGrounded to catch up to the NPC's final resting position. Does not stop the chain by itself - see stuckTimeoutBuffer.")]
    [SerializeField] private float arrivalGracePeriod = 0.25f;
    [Tooltip("Extra seconds added on top of the longest known recording for the current segment before the NPC is force-teleported to the next checkpoint. Recovers from a stuck/derailed replay (e.g. pinned by an obstacle) without permanently softlocking the chain.")]
    [SerializeField] private float stuckTimeoutBuffer = 1f;

    [Header("Collision")]
    [Tooltip("If both are assigned, collision between the player and this NPC is disabled - otherwise the player's body can physically bump the NPC off its recorded path, causing arrival checks to fail just short of the checkpoint.")]
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private Collider2D npcCollider;

    private int _currentIndex;
    private bool _running;
    private bool _waitingForArrival;
    private float _graceTimeRemaining;
    private bool _arrivalFailureLogged;
    private float _segmentElapsedTime;
    private float _segmentTimeoutSeconds;
    private NpcSuspicionController _suspicion;

    // Last recording played per segment index. On the next visit to the same segment
    // (e.g. after dying and restarting from the first checkpoint) that recording is
    // excluded from the random pick when alternatives exist, so each retry takes
    // different routes instead of repeating the exact run that just failed.
    private readonly Dictionary<int, NpcRecording> _lastPlayedBySegment = new Dictionary<int, NpcRecording>();

    public bool IsRunning => _running;
    public int CurrentCheckpointIndex => _currentIndex;
    /// <summary>True once the NPC has reached the final checkpoint of the chain.</summary>
    public bool IsChainComplete { get; private set; }

    /// <summary>Raised once when the NPC reaches the final checkpoint of the chain
    /// (the level goal). Not raised on arrival failure or external Stop().</summary>
    public event System.Action ChainCompleted;

    private void Awake()
    {
        if (playback == null)
        {
            playback = GetComponent<NpcCommandPlayback>();
        }

        if (playerCollider != null && npcCollider != null)
        {
            Physics2D.IgnoreCollision(playerCollider, npcCollider, true);
        }

        IgnoreDynamicObstacles();

        _suspicion = GetComponent<NpcSuspicionController>();
    }

    /// <summary>
    /// Recordings do not include other moving bodies. A non-lethal obstacle (any
    /// other CharacterMotor2D) sitting on the path would pin the NPC at a wall and
    /// fail arrival even though the recorded jumps/walks were valid. Monsters are
    /// excluded so their kill-on-touch collision with the NPC still fires.
    /// </summary>
    private void IgnoreDynamicObstacles()
    {
        if (npcCollider == null)
        {
            return;
        }

        CharacterMotor2D[] motors = FindObjectsByType<CharacterMotor2D>(FindObjectsSortMode.None);
        for (int i = 0; i < motors.Length; i++)
        {
            CharacterMotor2D otherMotor = motors[i];
            if (otherMotor == null || otherMotor == playback?.Motor || otherMotor.GetComponent<MonsterPatrolController>() != null)
            {
                continue;
            }

            Collider2D otherCollider = otherMotor.GetComponent<Collider2D>();
            if (otherCollider != null)
            {
                Physics2D.IgnoreCollision(npcCollider, otherCollider, true);
            }
        }
    }

    private void OnEnable()
    {
        if (playback != null)
        {
            playback.PlaybackCompleted += OnSegmentPlaybackCompleted;
        }
    }

    private void OnDisable()
    {
        if (playback != null)
        {
            playback.PlaybackCompleted -= OnSegmentPlaybackCompleted;
        }
    }

    private void Start()
    {
        if (autoStart)
        {
            BeginChain();
        }
    }

    public void BeginChain()
    {
        if (playback == null || checkpointChain.Count < 2)
        {
            Debug.LogWarning($"[{nameof(NpcProgressionController)}] Need a playback reference and at least 2 checkpoints in the chain.");
            return;
        }

        _currentIndex = 0;
        IsChainComplete = false;
        checkpointChain[0].SnapTo(playback.Body, playback.Motor);
        _running = true;
        PlayNextSegment();
    }

    public void Stop()
    {
        _running = false;
        _waitingForArrival = false;
        playback?.Stop();
    }

    /// <summary>
    /// Kills the NPC (e.g. it fell into a KillBorder): abandons the current segment,
    /// resets back to the first checkpoint, and restarts the whole chain. Route
    /// selection re-rolls on the way back up, avoiding each segment's previous pick
    /// where alternatives exist.
    /// </summary>
    public void Die()
    {
        Debug.Log($"[{nameof(NpcProgressionController)}] NPC died. Resetting to '{(checkpointChain.Count > 0 ? checkpointChain[0].Id : "?")}' and restarting chain.");

        _waitingForArrival = false;
        playback?.Stop();

        // Otherwise the NPC could respawn still flagged Suspicious from before it died.
        if (_suspicion != null)
        {
            _suspicion.ResetSuspicion();
        }

        BeginChain();
    }

    private void PlayNextSegment()
    {
        if (!_running)
        {
            return;
        }

        if (_currentIndex >= checkpointChain.Count - 1)
        {
            Debug.Log($"[{nameof(NpcProgressionController)}] Chain complete at checkpoint '{checkpointChain[_currentIndex].Id}'.");
            _running = false;
            IsChainComplete = true;
            ChainCompleted?.Invoke();
            return;
        }

        ProgressCheckpoint from = checkpointChain[_currentIndex];
        ProgressCheckpoint to = checkpointChain[_currentIndex + 1];

        List<NpcRecording> candidates = recordingLibrary
            .Where(r => r != null && r.StartCheckpointId == from.Id && r.EndCheckpointId == to.Id)
            .ToList();

        if (candidates.Count == 0)
        {
            Debug.LogError($"[{nameof(NpcProgressionController)}] No recording found for segment '{from.Id}' -> '{to.Id}'. Stopping.");
            _running = false;
            return;
        }

        // Watchdog timeout for this segment: the longest recording known for this
        // checkpoint pair (not just whichever one gets chosen below) plus a buffer,
        // so a short variant doesn't get judged against an unreasonably tight window.
        int longestTickCount = candidates.Max(r => r.Commands.Count);
        _segmentTimeoutSeconds = longestTickCount * Time.fixedDeltaTime + stuckTimeoutBuffer;
        _segmentElapsedTime = 0f;
        _arrivalFailureLogged = false;

        // Avoid replaying the same variant this segment used last time around, so a
        // respawned NPC takes different routes on its next attempt.
        if (candidates.Count > 1 && _lastPlayedBySegment.TryGetValue(_currentIndex, out NpcRecording lastPlayed))
        {
            candidates.Remove(lastPlayed);
        }

        NpcRecording chosen = candidates[Random.Range(0, candidates.Count)];
        _lastPlayedBySegment[_currentIndex] = chosen;
        Debug.Log($"[{nameof(NpcProgressionController)}] Segment '{from.Id}' -> '{to.Id}': playing '{chosen.name}' ({chosen.Commands.Count} ticks).");

        playback.PrepareAtCheckpoint(from);
        playback.SetRecording(chosen.Commands);
        playback.Play();
    }

    // Does not decide pass/fail itself - CharacterMotor2D.IsGrounded can still be one
    // tick stale relative to the NPC's actual final position at the instant playback
    // runs out (see class-level comment). Arms a short settle window instead; the real
    // check happens in FixedUpdate, after CharacterMotor2D has had a chance to refresh
    // grounded state against the up-to-date position.
    private void OnSegmentPlaybackCompleted()
    {
        if (!_running)
        {
            return;
        }

        _waitingForArrival = true;
        _graceTimeRemaining = arrivalGracePeriod;
    }

    private void FixedUpdate()
    {
        if (!_running)
        {
            return;
        }

        // Counts up whenever the segment is actively in progress, including the
        // post-playback window spent waiting for arrival - it only pauses when
        // playback itself is intentionally paused (e.g. NPC went Suspicious) or an
        // external system (e.g. a monster-avoidance reaction) is driving the motor
        // directly, not when the recording has simply finished. That's what lets this
        // watchdog catch the exact "commands ran out but NPC never arrived" stuck
        // state without also penalizing a brief, legitimate reaction jump.
        if (playback != null && !playback.IsPaused && !playback.IsExternallyControlled)
        {
            _segmentElapsedTime += Time.fixedDeltaTime;
        }

        if (_segmentElapsedTime >= _segmentTimeoutSeconds)
        {
            ForceAdvancePastStuckSegment();
            return;
        }

        if (!_waitingForArrival)
        {
            return;
        }

        ProgressCheckpoint expected = checkpointChain[_currentIndex + 1];

        if (expected.HasArrived(playback.Body, playback.Motor))
        {
            _waitingForArrival = false;
            Debug.Log($"[{nameof(NpcProgressionController)}] Reached checkpoint '{expected.Id}'.");
            expected.SnapTo(playback.Body, playback.Motor);
            _currentIndex++;
            PlayNextSegment();
            return;
        }

        // Playback already zeroed moveInput the instant it stopped (NpcCommandPlayback.
        // Stop()), so the NPC just sits idle here while it settles - it does not keep
        // drifting while we wait.
        _graceTimeRemaining -= Time.fixedDeltaTime;
        if (_graceTimeRemaining <= 0f && !_arrivalFailureLogged)
        {
            // Diagnostic only - the chain keeps waiting (and the watchdog above keeps
            // counting) instead of stopping here, since a slow arrival isn't
            // necessarily a stuck one.
            LogArrivalFailure(expected);
            _arrivalFailureLogged = true;
        }
    }

    /// <summary>
    /// Called when a segment has been in progress longer than the longest known
    /// recording for it plus stuckTimeoutBuffer, without the NPC arriving. Rather
    /// than leaving the chain permanently stalled, forcibly places the NPC at the
    /// next checkpoint and resumes as if it had arrived normally.
    /// </summary>
    private void ForceAdvancePastStuckSegment()
    {
        ProgressCheckpoint expected = checkpointChain[_currentIndex + 1];

        Debug.LogWarning(
            $"[{nameof(NpcProgressionController)}] Segment '{checkpointChain[_currentIndex].Id}' -> '{expected.Id}' exceeded its " +
            $"{_segmentTimeoutSeconds:F2}s timeout (longest known recording + buffer) without arriving. Teleporting NPC to '{expected.Id}' to avoid a softlock.");

        _waitingForArrival = false;
        playback.Stop();
        expected.SnapTo(playback.Body, playback.Motor);
        _currentIndex++;
        PlayNextSegment();
    }

    private void LogArrivalFailure(ProgressCheckpoint expected)
    {
        Rigidbody2D body = playback.Body;
        CharacterMotor2D motor = playback.Motor;

        Vector2 position = body != null ? body.position : Vector2.zero;
        Vector2 velocity = motor != null ? motor.Velocity : Vector2.zero;
        bool grounded = motor != null && motor.IsGrounded;
        Vector2 offset = position - expected.Anchor;
        Vector2 tolerance = expected.ArrivalHalfExtents;

        Debug.LogWarning(
            $"[{nameof(NpcProgressionController)}] Segment ended but NPC did not reach expected checkpoint '{expected.Id}' within {arrivalGracePeriod:F2}s grace window. " +
            $"position={position} velocity={velocity} grounded={grounded} offsetFromAnchor={offset} (arrivalHalfExtents={tolerance}). Still waiting - stuck-timeout watchdog will teleport it if this persists.");
    }
}
