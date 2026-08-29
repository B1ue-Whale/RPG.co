using UnityEngine;

/// <summary>
/// Reset zone for the player. Any collider belonging to the player that enters this
/// trigger is teleported back to startPoint, with velocity and jump/move transient
/// state cleared - the same reset primitives used elsewhere (ProgressCheckpoint.SnapTo,
/// NpcCommandPlayback.ResetToStart). Attach to a GameObject with any Collider2D set
/// as a trigger (e.g. a BoxCollider2D covering a pit or hazard area).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlayerResetZone : MonoBehaviour
{
    [Tooltip("Where the player is teleported to when this zone is touched.")]
    [SerializeField] private Transform startPoint;

    private void Reset()
    {
        // Sensible default when the component is first added in the editor.
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Resolve through the rigidbody so a child collider on the player still finds
        // the controller on the root object (same pattern as KillBorder).
        PlayerController player = other.attachedRigidbody != null
            ? other.attachedRigidbody.GetComponentInParent<PlayerController>()
            : other.GetComponentInParent<PlayerController>();

        if (player == null)
        {
            return;
        }

        if (startPoint == null)
        {
            Debug.LogWarning($"{nameof(PlayerResetZone)} on '{name}' has no startPoint assigned.", this);
            return;
        }

        Rigidbody2D body = other.attachedRigidbody != null ? other.attachedRigidbody : player.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            ProgressCheckpoint.TeleportRigidbody(body, startPoint.position);
        }

        player.GetComponent<CharacterMotor2D>()?.ResetTransientState();
    }
}
