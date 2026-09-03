using UnityEngine;

/// <summary>
/// Pure perception component: reports whether the real Player currently falls
/// within this NPC's facing-relative field of view, distance, and unobstructed
/// line of sight. Knows nothing about NPC state, awareness, or playback -
/// NpcSuspicionController drives it and decides what to do with the result. Call
/// <see cref="Sense"/> once per tick from that controller.
/// </summary>
public class NpcVisionSensor : MonoBehaviour
{
    [Header("Eye")]
    [Tooltip("Origin of vision checks. Defaults to this transform if unset.")]
    [SerializeField] private Transform eye;
    [Tooltip("Source of facing direction (left/right) used to orient the vision cone. Defaults to CharacterMotor2D on this GameObject.")]
    [SerializeField] private CharacterMotor2D motor;

    [Header("Vision Shape")]
    [Tooltip("Maximum distance a target can be seen from.")]
    [SerializeField] private float visionDistance = 8f;
    [Tooltip("Full field-of-view angle in degrees, centered on facing direction.")]
    [SerializeField, Range(0f, 360f)] private float visionAngle = 100f;
    [Tooltip("Layers that block line of sight to the Player (level geometry/walls). Must not include the Player or NPC layers themselves.")]
    [SerializeField] private LayerMask obstructionMask;

    [Header("Player Target")]
    [SerializeField] private Transform player;
    [Tooltip("Optional. If assigned, a hidden player is never detected.")]
    [SerializeField] private PlayerHideController playerHideController;

    public VisionDetection CurrentDetection { get; private set; } = VisionDetection.None;

    /// <summary>Maximum distance this sensor can see. Exposed so awareness scaling can use
    /// the edge of vision as its "far" reference without a second, duplicated field.</summary>
    public float VisionDistance => visionDistance;

    /// <summary>Origin of vision checks, in world space - the point distances are measured from.</summary>
    public Vector3 EyeWorldPosition => EyePosition;

    private Vector3 EyePosition => eye != null ? eye.position : transform.position;

    private void Awake()
    {
        if (motor == null)
        {
            motor = GetComponent<CharacterMotor2D>();
        }
    }

    /// <summary>
    /// Re-evaluates what is currently visible and returns it.
    /// </summary>
    public VisionDetection Sense()
    {
        VisionDetection detection = TrySensePlayer();

        CurrentDetection = detection;
        return detection;
    }

    private VisionDetection TrySensePlayer()
    {
        if (player == null || (playerHideController != null && playerHideController.IsHidden))
        {
            return VisionDetection.None;
        }

        return CanSee(player.position, checkObstruction: true)
            ? new VisionDetection(VisionTargetKind.Player, player.position)
            : VisionDetection.None;
    }

    private bool CanSee(Vector3 targetPosition, bool checkObstruction)
    {
        Vector2 origin = EyePosition;
        Vector2 toTarget = (Vector2)targetPosition - origin;
        float distance = toTarget.magnitude;
        if (distance > visionDistance)
        {
            return false;
        }

        if (distance > 0.0001f)
        {
            Vector2 direction = toTarget / distance;
            if (Vector2.Angle(FacingForward(), direction) > visionAngle * 0.5f)
            {
                return false;
            }

            if (checkObstruction)
            {
                RaycastHit2D hit = Physics2D.Raycast(origin, direction, distance, obstructionMask);
                if (hit.collider != null)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private Vector2 FacingForward()
    {
        int facing = motor != null ? motor.FacingDirection : 1;
        return facing >= 0 ? Vector2.right : Vector2.left;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = EyePosition;
        Vector2 forward = Application.isPlaying ? FacingForward() : Vector2.right;

        Gizmos.color = new Color(1f, 1f, 0f, 0.6f);
        Quaternion leftRot = Quaternion.Euler(0f, 0f, visionAngle * 0.5f);
        Quaternion rightRot = Quaternion.Euler(0f, 0f, -visionAngle * 0.5f);
        Gizmos.DrawLine(origin, origin + leftRot * (Vector3)forward * visionDistance);
        Gizmos.DrawLine(origin, origin + rightRot * (Vector3)forward * visionDistance);
        Gizmos.DrawLine(origin, origin + (Vector3)forward * visionDistance);
    }
}
