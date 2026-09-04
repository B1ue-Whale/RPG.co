using UnityEngine;

/// <summary>
/// First-pass Suspicious behavior layered on top of the existing Progressing
/// (checkpoint chain + recorded playback) system. Ticks NpcVisionSensor, accumulates
/// an awareness value from it, and on threshold pauses NpcCommandPlayback in place so
/// the NPC turns to face whatever it saw - without touching NpcProgressionController,
/// which simply never sees a PlaybackCompleted event while paused and stays put.
/// <para>
/// Deliberately simple for v1: no investigation movement, no pathfinding, no sound.
/// The NPC stays exactly where it paused, faces the last detected target, and resumes
/// playback from the exact same tick once awareness decays back down.
/// </para>
/// </summary>
// Runs after CharacterMotor2D (order 0) for the same reason NpcProgressionController
// does (see its class comment): IsGrounded must reflect this tick's actual position,
// not a stale one, when deciding whether to pause immediately or wait for landing.
// +50 (not +100, NpcProgressionController's order) so it deterministically runs before
// NpcProgressionController: a Pause() called this tick must be visible to
// NpcProgressionController's own FixedUpdate in the same tick, not one tick later.
[DefaultExecutionOrder(50)]
public class NpcSuspicionController : MonoBehaviour
{
    private enum State
    {
        Progressing,
        // Awareness crossed suspiciousEnterThreshold while airborne. Recorded movement
        // keeps playing untouched until grounded, so a mid-jump pause can't corrupt the
        // replay trajectory.
        SuspiciousPending,
        Suspicious
    }

    [SerializeField] private NpcVisionSensor vision;
    [SerializeField] private NpcCommandPlayback playback;
    [SerializeField] private CharacterMotor2D motor;

    [Header("Awareness")]
    [Tooltip("Awareness gained per second while a valid target (Player or Bug/BW) is visible.")]
    [SerializeField] private float awarenessGainRate = 1f;
    [Tooltip("Awareness lost per second while nothing is visible.")]
    [SerializeField] private float awarenessDecayRate = 0.5f;
    [Tooltip("Multiplier on the gain rate when the target is at (or inside) awarenessCloseDistance. Scales linearly down to 1x at the edge of vision, so a close target is noticed faster than one barely in range. Only affects gain - decay is unchanged.")]
    [SerializeField] private float awarenessCloseMultiplier = 2f;
    [Tooltip("Distance in world units (1 tile = 1 unit) at or below which the full awarenessCloseMultiplier applies.")]
    [SerializeField] private float awarenessCloseDistance = 3f;
    [Tooltip("Awareness (0..1) at which Progressing enters Suspicious.")]
    [SerializeField, Range(0f, 1f)] private float suspiciousEnterThreshold = 0.75f;
    [Tooltip("Awareness (0..1) at which Suspicious returns to Progressing. Keep below the enter threshold to avoid flickering between states.")]
    [SerializeField, Range(0f, 1f)] private float suspiciousExitThreshold = 0.35f;

    [Header("Suspicion Bar")]
    [SerializeField] private Vector3 suspicionBarOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Vector2 suspicionBarSize = new Vector2(1f, 0.15f);
    [SerializeField] private Color suspicionBarBackground = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color suspicionBarFill = new Color(1f, 0.6f, 0f, 1f);

    private State _state = State.Progressing;
    private WorldSpaceProgressBar _suspicionBar;
    private bool _suspicionMaxed;

    public float Awareness { get; private set; }
    public bool IsSuspicious => _state == State.Suspicious;

    /// <summary>
    /// Raised exactly once, the first time Awareness reaches 1 (the meter is full).
    /// Listeners (e.g. the level lose condition) treat this as "the NPC has fully
    /// noticed something is wrong". Re-armed by <see cref="ResetSuspicion"/>.
    /// </summary>
    public event System.Action SuspicionMaxed;

    /// <summary>
    /// Clears all suspicion state (awareness, Suspicious/pending state, bar). Called
    /// when the NPC dies and respawns so it doesn't come back still suspicious.
    /// </summary>
    public void ResetSuspicion()
    {
        Awareness = 0f;
        _state = State.Progressing;
        _suspicionMaxed = false;
        if (_suspicionBar != null)
        {
            _suspicionBar.SetFill(0f);
            _suspicionBar.SetVisible(false);
        }
    }

    private void Awake()
    {
        _suspicionBar = WorldSpaceProgressBar.Create(transform, suspicionBarOffset, suspicionBarSize, suspicionBarBackground, suspicionBarFill);

        if (vision == null)
        {
            vision = GetComponent<NpcVisionSensor>();
        }
        if (playback == null)
        {
            playback = GetComponent<NpcCommandPlayback>();
        }
        if (motor == null)
        {
            motor = GetComponent<CharacterMotor2D>();
        }
    }

    private void FixedUpdate()
    {
        if (vision == null || playback == null || motor == null || !playback.IsPlaying || playback.IsForceFrozen)
        {
            return;
        }

        VisionDetection detection = vision.Sense();
        bool hasTarget = detection.Kind != VisionTargetKind.None;

        float rate = hasTarget
            ? awarenessGainRate * GetProximityGainMultiplier(detection.Position)
            : -awarenessDecayRate;
        Awareness = Mathf.Clamp01(Awareness + rate * Time.fixedDeltaTime);

        _suspicionBar.SetVisible(Awareness > 0f);
        _suspicionBar.SetFill(Awareness);

        if (!_suspicionMaxed && Awareness >= 1f)
        {
            _suspicionMaxed = true;
            SuspicionMaxed?.Invoke();
        }

        switch (_state)
        {
            case State.Progressing:
                if (Awareness >= suspiciousEnterThreshold)
                {
                    _state = motor.IsGrounded ? EnterSuspicious(detection) : State.SuspiciousPending;
                }
                break;

            case State.SuspiciousPending:
                if (Awareness < suspiciousEnterThreshold)
                {
                    // Lost the target again before landing - never actually went Suspicious.
                    _state = State.Progressing;
                }
                else if (motor.IsGrounded)
                {
                    _state = EnterSuspicious(detection);
                }
                break;

            case State.Suspicious:
                if (hasTarget)
                {
                    FaceTarget(detection.Position);
                }
                else if (Awareness <= suspiciousExitThreshold)
                {
                    _state = State.Progressing;
                    playback.Resume();
                }
                break;
        }
    }

    /// <summary>
    /// 1x at the edge of the vision cone, ramping linearly up to awarenessCloseMultiplier
    /// at awarenessCloseDistance and staying there for anything nearer. Measured from the
    /// sensor's eye, the same origin the vision checks themselves use, and bounded by the
    /// sensor's own vision distance so the two can never drift apart.
    /// </summary>
    private float GetProximityGainMultiplier(Vector3 targetPosition)
    {
        float far = vision.VisionDistance;
        float near = Mathf.Max(0f, awarenessCloseDistance);

        if (far <= near)
        {
            // The whole cone is within "close" range - no meaningful ramp to draw.
            return awarenessCloseMultiplier;
        }

        float distance = Vector2.Distance(vision.EyeWorldPosition, targetPosition);
        float closeness = Mathf.InverseLerp(far, near, distance);

        return Mathf.Lerp(1f, awarenessCloseMultiplier, closeness);
    }

    private State EnterSuspicious(VisionDetection detection)
    {
        playback.Pause();
        FaceTarget(detection.Position);
        return State.Suspicious;
    }

    private void FaceTarget(Vector3 targetPosition)
    {
        float dx = targetPosition.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.01f)
        {
            motor.SetFacing(dx > 0f ? 1 : -1);
        }
    }
}
