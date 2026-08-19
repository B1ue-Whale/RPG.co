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
    [Tooltip("After a segment's commands run out, how long to keep checking for a valid arrival before giving up. Covers the tick or two it takes CharacterMotor2D's IsGrounded to catch up to the NPC's final resting position.")]
    [SerializeField] private float arrivalGracePeriod = 0.25f;

    [Header("Collision")]
    [Tooltip("If both are assigned, collision between the player and this NPC is disabled - otherwise the player's body can physically bump the NPC off its recorded path, causing arrival checks to fail just short of the checkpoint.")]
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private Collider2D npcCollider;

    private int _currentIndex;
    private bool _running;
    private bool _waitingForArrival;
    private float _graceTimeRemaining;

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
        playback?.Stop();
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

        NpcRecording chosen = candidates[Random.Range(0, candidates.Count)];
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
        if (_graceTimeRemaining <= 0f)
        {
            _waitingForArrival = false;
            LogArrivalFailure(expected);
            _running = false;
        }
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

        Debug.LogError(
            $"[{nameof(NpcProgressionController)}] Segment ended but NPC did not reach expected checkpoint '{expected.Id}' within {arrivalGracePeriod:F2}s grace window. " +
            $"position={position} velocity={velocity} grounded={grounded} offsetFromAnchor={offset} (arrivalHalfExtents={tolerance}). Stopping chain.");
    }
}
