using UnityEngine;

/// <summary>
/// A named waypoint in the Stage 1 checkpoint chain (CP0, CP1, CP2, ...). The anchor is
/// this transform's position; arrival is a small box around it, not pixel-perfect
/// alignment - see <see cref="HasArrived"/>.
/// </summary>
public class ProgressCheckpoint : MonoBehaviour
{
    [Tooltip("Unique id used by NpcRecording assets to reference this checkpoint. Must be unique across the chain.")]
    [SerializeField] private string checkpointId;

    [Tooltip("Half-size of the arrival area around this transform's position (x = horizontal, y = vertical tolerance).")]
    [SerializeField] private Vector2 arrivalHalfExtents = new Vector2(0.5f, 0.5f);

    [Header("Play Mode Visualization")]
    [Tooltip("Draw the anchor + arrival area with LineRenderers while playing, so checkpoints are visible in the Game view - Scene-view gizmos alone don't show up there.")]
    [SerializeField] private bool showInPlayMode = true;
    [SerializeField] private Color visualColor = Color.cyan;
    [SerializeField] private float anchorMarkerRadius = 0.08f;
    [SerializeField] private int sortingOrder = 100;

    public string Id => checkpointId;
    public Vector2 Anchor => transform.position;
    public Vector2 ArrivalHalfExtents => arrivalHalfExtents;

    /// <summary>
    /// True when body is grounded and within arrivalHalfExtents of the anchor on both
    /// axes. Intentionally not exact-position - small drift within the arrival box
    /// still counts.
    /// </summary>
    public bool HasArrived(Rigidbody2D body, CharacterMotor2D motor)
    {
        if (body == null || motor == null || !motor.IsGrounded)
        {
            return false;
        }

        Vector2 offset = body.position - Anchor;
        return Mathf.Abs(offset.x) <= arrivalHalfExtents.x && Mathf.Abs(offset.y) <= arrivalHalfExtents.y;
    }

    /// <summary>
    /// Snaps body to the exact anchor with zero velocity/rotation and clears motor's
    /// stale move/jump/coyote state. Used both to place a player before recording and
    /// to normalize an NPC after a successful segment.
    /// </summary>
    public void SnapTo(Rigidbody2D body, CharacterMotor2D motor)
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = Anchor;
            body.rotation = 0f;
        }

        motor?.ResetTransientState();
    }

    private void Awake()
    {
        if (showInPlayMode)
        {
            BuildRuntimeVisual();
        }
    }

    // Runtime-only child LineRenderers instead of Gizmos, since gizmos only draw in the
    // Scene view (and only when selected via OnDrawGizmosSelected) - not in the Game
    // view during Play, which is what "visible from play mode" needs.
    private void BuildRuntimeVisual()
    {
        var material = new Material(Shader.Find("Sprites/Default"));
        Vector3 center = transform.position;

        var box = CreateLine("CheckpointVisual_Box", material, loop: true);
        Vector2 half = arrivalHalfExtents;
        box.positionCount = 5;
        box.SetPosition(0, center + new Vector3(-half.x, -half.y, 0f));
        box.SetPosition(1, center + new Vector3(half.x, -half.y, 0f));
        box.SetPosition(2, center + new Vector3(half.x, half.y, 0f));
        box.SetPosition(3, center + new Vector3(-half.x, half.y, 0f));
        box.SetPosition(4, center + new Vector3(-half.x, -half.y, 0f));

        var marker = CreateLine("CheckpointVisual_Anchor", material, loop: true);
        const int segments = 16;
        marker.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments * Mathf.PI * 2f;
            marker.SetPosition(i, center + new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f) * anchorMarkerRadius);
        }
    }

    private LineRenderer CreateLine(string childName, Material material, bool loop)
    {
        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);

        var line = go.AddComponent<LineRenderer>();
        line.material = material;
        line.useWorldSpace = true;
        line.loop = loop;
        line.widthMultiplier = 0.04f;
        line.startColor = visualColor;
        line.endColor = visualColor;
        line.sortingOrder = sortingOrder;
        line.numCapVertices = 2;
        return line;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, (Vector3)(arrivalHalfExtents * 2f));
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.position, anchorMarkerRadius);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            checkpointId = name;
        }
    }
#endif
}
