using UnityEngine;

/// <summary>
/// Kill zone for NPCs. Any NPC whose collider enters this trigger dies: its
/// NpcProgressionController resets it back to the first checkpoint and restarts the
/// chain (re-rolling route choices per segment). Attach to a GameObject with any
/// Collider2D set as a trigger (e.g. a BoxCollider2D stretched below the level).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class KillBorder : MonoBehaviour
{
    private void Reset()
    {
        // Sensible default when the component is first added in the editor.
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Resolve through the rigidbody so child colliders on the NPC still find the
        // controller on the root object.
        NpcProgressionController npc = other.attachedRigidbody != null
            ? other.attachedRigidbody.GetComponentInParent<NpcProgressionController>()
            : other.GetComponentInParent<NpcProgressionController>();

        if (npc != null)
        {
            npc.Die();
        }
    }
}
