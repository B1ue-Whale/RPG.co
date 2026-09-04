using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
    [Tooltip("Extra seconds added on top of the longest known recording for the current segment before the outer safety-net watchdog force-teleports the NPC to the next checkpoint. Only guards Playback and ArrivalGrace - a backstop for playback itself never finishing (e.g. a future bug leaves it stuck mid-recording). Recovery below always gets its own full, independent recoveryTimeout regardless of this value.")]
    [SerializeField] private float stuckTimeoutBuffer = 1f;

    [Header("Recovery")]
    [Tooltip("If the NPC hasn't arrived by the end of arrivalGracePeriod, it enters recovery: walks directly toward the next checkpoint and jumps over simple obstacles in its way. Not pathfinding - just enough to shrug off small replay drift or a slightly bad end position.")]
    [SerializeField] private float recoveryTimeout = 2f;
    [Tooltip("Layers that count as walls for the recovery jump-over check. Usually the same Ground layer the motor uses.")]
    [SerializeField] private LayerMask recoveryObstacleLayer;
    [Tooltip("How far ahead to check for a wall while recovering.")]
    [SerializeField] private float recoveryWallCheckDistance = 0.15f;

    [Header("Death")]
    [Tooltip("Seconds the NPC stays hidden after the death clip finishes, before respawning at the first checkpoint.")]
    [SerializeField] private float respawnDelay = 4f;

    [Header("Collision")]
    [Tooltip("If both are assigned, collision between the player and this NPC is disabled - otherwise the player's body can physically bump the NPC off its recorded path, causing arrival checks to fail just short of the checkpoint.")]
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private Collider2D npcCollider;

    private enum SegmentPhase
    {
        // Recorded commands are still being consumed; waiting on PlaybackCompleted.
        Playback,
        // Commands ran out; briefly waiting for CharacterMotor2D.IsGrounded to catch
        // up before judging arrival (see class-level comment on execution order).
        ArrivalGrace,
        // Arrival didn't succeed within the grace window; actively walking/jumping
        // toward the next checkpoint instead of idly waiting.
        Recovering
    }

    private int _currentIndex;
    private bool _running;
    private SegmentPhase _phase;
    private float _graceTimeRemaining;
    private float _segmentElapsedTime;
    private float _segmentTimeoutSeconds;
    private float _recoveryElapsedTime;
    private NpcSuspicionController _suspicion;
    private PlayerAnimator _visualAnimator;
    private NpcMonsterJumpReaction _jumpReaction;

    // Last recording played per segment index. On the next visit to the same segment
    // (e.g. after dying and restarting from the first checkpoint) that recording is
    // excluded from the random pick when alternatives exist, so each retry takes
    // different routes instead of repeating the exact run that just failed.
    private readonly Dictionary<int, NpcRecording> _lastPlayedBySegment = new Dictionary<int, NpcRecording>();

    // Konami code easter egg. Deliberately tucked in here rather than its own
    // component so it doesn't show up as a discoverable script/GameObject on its
    // own - no Inspector fields, no scene wiring, just a key sequence listener that
    // rides along on whichever NPC happens to own this controller.
    private static readonly Key[] KonamiSequence =
    {
        Key.UpArrow, Key.UpArrow, Key.DownArrow, Key.DownArrow,
        Key.LeftArrow, Key.RightArrow, Key.LeftArrow, Key.RightArrow,
        Key.B, Key.A
    };
    private const string KonamiSpriteResourceName = "dothede";
    private const string KonamiSfxResourceName = "FreddyJumpscare";
    private const float KonamiDisplaySeconds = 1f;
    private const float KonamiSpriteScreenFraction = 1.2f;

    private int _konamiProgress;
    private Sprite _konamiSprite;
    private AudioClip _konamiSfx;
    private Coroutine _konamiRoutine;

    public bool IsRunning => _running;
    public int CurrentCheckpointIndex => _currentIndex;
    /// <summary>The ordered checkpoint chain, exposed read-only so systems that care about
    /// where the NPC is heading (e.g. BugZone's spawn route forecast) can read the upcoming
    /// anchors without duplicating the chain or driving it.</summary>
    public IReadOnlyList<ProgressCheckpoint> CheckpointChain => checkpointChain;
    /// <summary>True once the NPC has reached the final checkpoint of the chain.</summary>
    public bool IsChainComplete { get; private set; }
    /// <summary>True while the death clip is playing and respawn has not started yet.</summary>
    public bool IsDying { get; private set; }

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
        _visualAnimator = GetComponent<PlayerAnimator>();
        _jumpReaction = GetComponent<NpcMonsterJumpReaction>();
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

        CharacterMotor2D[] motors = FindObjectsByType<CharacterMotor2D>();
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
        _phase = SegmentPhase.Playback;
        playback?.Stop();
    }

    /// <summary>
    /// Kills the NPC (e.g. it fell into a KillBorder or was hit by a monster):
    /// plays the death clip in place, hides the sprite, waits, then resets back to
    /// the first checkpoint and restarts the whole chain. Route selection re-rolls
    /// on the way back up, avoiding each segment's previous pick where alternatives exist.
    /// </summary>
    public void Die()
    {
        if (IsDying)
        {
            return;
        }

        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        IsDying = true;
        _running = false;
        _phase = SegmentPhase.Playback;

        // Cancel any in-progress reaction jump BEFORE Stop(). If the NPC dies
        // mid-reaction, NpcMonsterJumpReaction._reacting/_hasLeftGround must be reset
        // here too, not just NpcCommandPlayback.IsExternallyControlled (which Stop()
        // clears) - otherwise the reaction component keeps zeroing moveInput every tick
        // forever, even after playback itself is free to move again.
        _jumpReaction?.Cancel();
        playback?.Stop();
        playback?.ForceFreeze();

        if (npcCollider != null)
        {
            npcCollider.enabled = false;
        }

        // Hide the suspicion bar immediately so it doesn't linger over the corpse.
        if (_suspicion != null)
        {
            _suspicion.ResetSuspicion();
        }

        _visualAnimator?.SetDead(true);

        float clipLength = _visualAnimator != null ? _visualAnimator.DeathClipLength : 0.875f;
        Debug.Log($"[{nameof(NpcProgressionController)}] NPC died. Playing death animation ({clipLength:F2}s), then hidden for {respawnDelay:F2}s before resetting to '{(checkpointChain.Count > 0 ? checkpointChain[0].Id : "?")}'.");
        yield return new WaitForSeconds(clipLength);

        _visualAnimator?.SetVisible(false);
        yield return new WaitForSeconds(respawnDelay);

        ReviveStompedMonsters();

        _visualAnimator?.SetDead(false);

        if (npcCollider != null)
        {
            npcCollider.enabled = true;
        }

        IsDying = false;
        BeginChain();
        _visualAnimator?.SetVisible(true);
    }

    /// <summary>
    /// Brings back any monster this NPC previously stomped, so a death/restart
    /// doesn't leave the level permanently missing whatever it killed on the way.
    /// Scoped to MonsterPatrolController.IsDead specifically (not just "inactive")
    /// so a monster switched off for some unrelated reason is left alone, and a
    /// still-alive, still-patrolling monster is never touched by this.
    /// </summary>
    private void ReviveStompedMonsters()
    {
        MonsterPatrolController[] monsters = FindObjectsByType<MonsterPatrolController>(FindObjectsInactive.Include);
        for (int i = 0; i < monsters.Length; i++)
        {
            if (monsters[i] != null && monsters[i].IsDead)
            {
                monsters[i].ResetMonster();
            }
        }
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

        List<NpcRecording> candidates = CollectSegmentRecordings(_currentIndex);

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
        _phase = SegmentPhase.Playback;

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

    /// <summary>
    /// Every recording that covers the segment starting at checkpoint index
    /// <paramref name="fromIndex"/>. Empty when the index is out of range or no recording
    /// matches that checkpoint pair.
    /// </summary>
    private List<NpcRecording> CollectSegmentRecordings(int fromIndex)
    {
        if (fromIndex < 0 || fromIndex >= checkpointChain.Count - 1)
        {
            return new List<NpcRecording>();
        }

        ProgressCheckpoint from = checkpointChain[fromIndex];
        ProgressCheckpoint to = checkpointChain[fromIndex + 1];

        if (from == null || to == null)
        {
            return new List<NpcRecording>();
        }

        return recordingLibrary
            .Where(r => r != null && r.StartCheckpointId == from.Id && r.EndCheckpointId == to.Id)
            .ToList();
    }

    /// <summary>
    /// Rough duration of the segment starting at checkpoint index
    /// <paramref name="fromIndex"/>, as the average length of the recordings available for
    /// it. Which variant actually gets picked is decided later and at random, so this is an
    /// estimate by design - good enough to order future checkpoints in time, not something
    /// to schedule against. Returns 0 when the segment has no recordings.
    /// </summary>
    public float EstimateSegmentSeconds(int fromIndex)
    {
        List<NpcRecording> recordings = CollectSegmentRecordings(fromIndex);
        if (recordings.Count == 0)
        {
            return 0f;
        }

        long totalTicks = 0;
        for (int i = 0; i < recordings.Count; i++)
        {
            totalTicks += recordings[i].Commands.Count;
        }

        return (float)totalTicks / recordings.Count * Time.fixedDeltaTime;
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

        _phase = SegmentPhase.ArrivalGrace;
        _graceTimeRemaining = arrivalGracePeriod;
    }

    private void FixedUpdate()
    {
        if (!_running)
        {
            return;
        }

        // True while nothing is intentionally holding things up - not Suspicious,
        // not a reaction (e.g. monster-avoidance jump) currently driving the motor,
        // and not a gadget freeze (e.g. Garry's Gun). Used to keep both the outer
        // watchdog and the arrival-grace/recovery timers from burning down while
        // something else legitimately has control for a moment.
        bool activelyProgressing = playback != null
            && !playback.IsPaused
            && !playback.IsExternallyControlled
            && !playback.IsForceFrozen;

        // Outer safety net for Playback + ArrivalGrace only - guards against playback
        // itself somehow never completing. Deliberately stops advancing (and stops
        // being checked) once Recovering begins: recovery's own dedicated timeout
        // owns that phase completely from then on, so there is no implicit dependency
        // between stuckTimeoutBuffer, arrivalGracePeriod, and recoveryTimeout - a
        // recovery attempt always gets its full configured budget regardless of how
        // long Playback/ArrivalGrace happened to take first.
        if (_phase != SegmentPhase.Recovering)
        {
            if (activelyProgressing)
            {
                _segmentElapsedTime += Time.fixedDeltaTime;
            }

            if (_segmentElapsedTime >= _segmentTimeoutSeconds)
            {
                ForceAdvancePastStuckSegment();
                return;
            }
        }

        switch (_phase)
        {
            case SegmentPhase.ArrivalGrace:
                TickArrivalGrace(activelyProgressing);
                break;

            case SegmentPhase.Recovering:
                TickRecovery(activelyProgressing);
                break;
        }
    }

    private void TickArrivalGrace(bool activelyProgressing)
    {
        ProgressCheckpoint expected = checkpointChain[_currentIndex + 1];

        if (expected.HasArrived(playback.Body, playback.Motor))
        {
            AdvanceToNextCheckpoint(expected);
            return;
        }

        if (!activelyProgressing)
        {
            // A reaction currently owns the motor - don't burn the grace window while
            // it's in control.
            return;
        }

        // Playback already zeroed moveInput the instant it stopped (NpcCommandPlayback.
        // Stop()), so the NPC just sits idle here while it settles - it does not keep
        // drifting while we wait.
        _graceTimeRemaining -= Time.fixedDeltaTime;
        if (_graceTimeRemaining <= 0f)
        {
            LogArrivalFailure(expected);
            BeginRecovery(expected);
        }
    }

    private void BeginRecovery(ProgressCheckpoint expected)
    {
        _phase = SegmentPhase.Recovering;
        _recoveryElapsedTime = 0f;
        Debug.Log($"[{nameof(NpcProgressionController)}] Entering recovery toward checkpoint '{expected.Id}'.");
    }

    /// <summary>
    /// Simple, non-pathfinding recovery: walk directly toward the next checkpoint's
    /// anchor and jump over anything immediately blocking that direction. Meant to
    /// shrug off small replay drift or a slightly bad end position, not replace the
    /// recorded route - if it can't get there within recoveryTimeout, ForceAdvancePastStuckSegment
    /// takes over.
    /// </summary>
    private void TickRecovery(bool activelyProgressing)
    {
        ProgressCheckpoint expected = checkpointChain[_currentIndex + 1];

        if (expected.HasArrived(playback.Body, playback.Motor))
        {
            AdvanceToNextCheckpoint(expected);
            return;
        }

        if (!activelyProgressing)
        {
            // e.g. the monster-jump-reaction is mid-jump and currently owns the motor.
            // Let it finish instead of fighting it, and don't spend recovery's own
            // budget while it's in control.
            return;
        }

        _recoveryElapsedTime += Time.fixedDeltaTime;
        if (_recoveryElapsedTime >= recoveryTimeout)
        {
            ForceAdvancePastStuckSegment();
            return;
        }

        CharacterMotor2D motor = playback.Motor;
        float dx = expected.Anchor.x - playback.Body.position.x;
        int direction = dx > 0.01f ? 1 : (dx < -0.01f ? -1 : 0);

        motor.SetMoveInput(direction);

        if (direction != 0 && motor.IsGrounded && IsWallAhead(direction))
        {
            motor.RequestJump();
        }
    }

    /// <summary>
    /// Two short rays (foot and mid height) cast from the leading edge of npcCollider,
    /// same pattern as Monster's own wall check. A hit with a mostly-horizontal normal
    /// counts as a wall worth jumping over.
    /// </summary>
    private bool IsWallAhead(int direction)
    {
        if (npcCollider == null)
        {
            return false;
        }

        const float skin = 0.02f;
        Bounds bounds = npcCollider.bounds;
        float frontX = direction > 0 ? bounds.max.x : bounds.min.x;
        Vector2 dir = Vector2.right * direction;
        float length = skin + recoveryWallCheckDistance;

        Vector2 footOrigin = new Vector2(frontX - direction * skin, bounds.min.y + skin);
        Vector2 midOrigin = new Vector2(frontX - direction * skin, bounds.center.y);

        return IsWallHit(Physics2D.Raycast(footOrigin, dir, length, recoveryObstacleLayer))
            || IsWallHit(Physics2D.Raycast(midOrigin, dir, length, recoveryObstacleLayer));
    }

    private static bool IsWallHit(RaycastHit2D hit)
    {
        return hit.collider != null && Mathf.Abs(hit.normal.x) > 0.5f;
    }

    private void AdvanceToNextCheckpoint(ProgressCheckpoint expected)
    {
        Debug.Log($"[{nameof(NpcProgressionController)}] Reached checkpoint '{expected.Id}'.");
        expected.SnapTo(playback.Body, playback.Motor);
        _currentIndex++;
        PlayNextSegment();
    }

    /// <summary>
    /// Final fallback: either the outer segment watchdog or recovery's own dedicated
    /// timeout gave up on this segment. Rather than leaving the chain permanently
    /// stalled, forcibly places the NPC at the next checkpoint and resumes as if it
    /// had arrived normally.
    /// </summary>
    private void ForceAdvancePastStuckSegment()
    {
        ProgressCheckpoint expected = checkpointChain[_currentIndex + 1];

        Debug.LogWarning(
            $"[{nameof(NpcProgressionController)}] Segment '{checkpointChain[_currentIndex].Id}' -> '{expected.Id}' could not reach arrival " +
            $"(phase={_phase}) within its timeout budget. Teleporting NPC to '{expected.Id}' to avoid a softlock.");

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
            $"position={position} velocity={velocity} grounded={grounded} offsetFromAnchor={offset} (arrivalHalfExtents={tolerance}). Entering recovery.");
    }

    private void Update()
    {
        TickKonamiListener();
    }

    /// <summary>
    /// Advances (or resets) progress through KonamiSequence based on whichever key was
    /// pressed this frame. Uses wasPressedThisFrame, not isPressed, so holding a key down
    /// cannot rack up repeated matches on the same press.
    /// </summary>
    private void TickKonamiListener()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        Key expected = KonamiSequence[_konamiProgress];
        if (keyboard[expected].wasPressedThisFrame)
        {
            _konamiProgress++;
            if (_konamiProgress >= KonamiSequence.Length)
            {
                _konamiProgress = 0;
                TriggerKonamiEasterEgg();
            }

            return;
        }

        // Any other key press breaks the streak, except when it happens to be the
        // correct first key of a fresh attempt - otherwise a mistyped code could never
        // be immediately retried without an unrelated key in between.
        for (int i = 0; i < keyboard.allKeys.Count; i++)
        {
            if (keyboard.allKeys[i].wasPressedThisFrame)
            {
                _konamiProgress = keyboard.allKeys[i].keyCode == KonamiSequence[0] ? 1 : 0;
                break;
            }
        }
    }

    private void TriggerKonamiEasterEgg()
    {
        if (_konamiRoutine != null)
        {
            // Already showing (or about to) - let the current one finish rather than
            // stacking a second popup on top of it.
            return;
        }

        _konamiRoutine = StartCoroutine(ShowKonamiEasterEgg());
    }

    private IEnumerator ShowKonamiEasterEgg()
    {
        if (_konamiSprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>(KonamiSpriteResourceName);
            if (texture == null)
            {
                Debug.LogWarning($"[{nameof(NpcProgressionController)}] Konami easter egg triggered but '{KonamiSpriteResourceName}' was not found under a Resources folder.");
                _konamiRoutine = null;
                yield break;
            }

            _konamiSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

        if (_konamiSfx == null)
        {
            _konamiSfx = Resources.Load<AudioClip>(KonamiSfxResourceName);
            if (_konamiSfx == null)
            {
                Debug.LogWarning($"[{nameof(NpcProgressionController)}] Konami easter egg triggered but '{KonamiSfxResourceName}' was not found under a Resources folder.");
            }
        }

        GameObject canvasObject = new GameObject("KonamiEasterEggCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        canvasObject.AddComponent<CanvasScaler>();

        GameObject imageObject = new GameObject("Dothede");
        imageObject.transform.SetParent(canvasObject.transform, false);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = _konamiSprite;
        image.raycastTarget = false;
        image.preserveAspect = true;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        float size = Mathf.Min(Screen.width, Screen.height) * KonamiSpriteScreenFraction;
        rect.sizeDelta = new Vector2(size, size);

        if (_konamiSfx != null)
        {
            // Parented to the same object the sprite lives on, so destroying it below
            // cuts the sfx off too if the clip happens to outlast the display window -
            // "while visible" means the sound should not survive the sprite.
            AudioSource audioSource = canvasObject.AddComponent<AudioSource>();
            audioSource.clip = _konamiSfx;
            audioSource.Play();
        }

        yield return new WaitForSeconds(KonamiDisplaySeconds);

        Destroy(canvasObject);
        _konamiRoutine = null;
    }
}
