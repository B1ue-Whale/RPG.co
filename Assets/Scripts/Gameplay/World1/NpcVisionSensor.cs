using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure perception component: reports whether the real Player or a visible Bug/BW
/// tile currently falls within this NPC's facing-relative field of view, distance,
/// and unobstructed line of sight. Knows nothing about NPC state, awareness, or
/// playback - NpcSuspicionController drives it and decides what to do with the
/// result. Call <see cref="Sense"/> once per tick from that controller.
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
    [Tooltip("Layers that block line of sight to the Player (level geometry/walls). Must not include the Player or NPC layers themselves. Not applied to Bug/BW tiles - see CanSee's checkObstruction param.")]
    [SerializeField] private LayerMask obstructionMask;

    [Header("Player Target")]
    [SerializeField] private Transform player;
    [Tooltip("Optional. If assigned, a hidden player is never detected.")]
    [SerializeField] private PlayerHideController playerHideController;

    [Header("Bug/BW Targets")]
    [Tooltip("BugZones whose infected cells count as visible Bug/BW targets.")]
    [SerializeField] private List<BugZone> bugZones = new List<BugZone>();

    public VisionDetection CurrentDetection { get; private set; } = VisionDetection.None;

    private Vector3 EyePosition => eye != null ? eye.position : transform.position;

    private void Awake()
    {
        if (motor == null)
        {
            motor = GetComponent<CharacterMotor2D>();
        }
    }

    /// <summary>
    /// Re-evaluates what is currently visible and returns it. Player takes priority
    /// over Bug/BW when both are visible.
    /// </summary>
    public VisionDetection Sense()
    {
        VisionDetection detection = TrySensePlayer();
        if (detection.Kind == VisionTargetKind.None)
        {
            detection = TrySenseBug();
        }

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

    private VisionDetection TrySenseBug()
    {
        for (int i = 0; i < bugZones.Count; i++)
        {
            BugZone zone = bugZones[i];
            if (zone == null)
            {
                continue;
            }

            IReadOnlyList<Vector3Int> cells = zone.InfectedCells;
            for (int c = 0; c < cells.Count; c++)
            {
                Vector3 world = zone.GetCellWorldCenter(cells[c]);
                // Bug/BW tiles are marked on the same tilemap used as ground/wall
                // geometry, so their own cell is inevitably obstruction geometry - a
                // wall raycast to it would just clip itself. Distance/FOV still apply.
                if (CanSee(world, checkObstruction: false))
                {
                    return new VisionDetection(VisionTargetKind.Bug, world);
                }
            }
        }

        return VisionDetection.None;
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
