using UnityEngine;

/// Simple patrol brain for a walking monster. Feeds move input into the shared
/// <see cref="CharacterMotor2D"/> so all acceleration/gravity behavior matches
/// other characters. The monster walks in one direction until it detects an
/// obstacle (a wall/block ahead) or a ledge (no ground ahead), then turns around.
[RequireComponent(typeof(CharacterMotor2D))]
[RequireComponent(typeof(Collider2D))]
public class MonsterPatrolController : MonoBehaviour
{
    private enum StartDirection
    {
        Right,
        Left
    }

    [Header("Patrol")]
    [Tooltip("Direction the monster starts walking in.")]
    [SerializeField] private StartDirection startDirection = StartDirection.Right;
    [Tooltip("Layers that count as walls and ground for obstacle/ledge detection. Usually the same Ground layer the motor uses.")]
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Wall Check")]
    [Tooltip("How far ahead of the collider's leading edge to look for a wall, in units.")]
    [SerializeField] private float wallCheckDistance = 0.1f;

    [Header("Ledge Check")]
    [Tooltip("Whether the monster turns around at ledges. Disable to let it walk off edges.")]
    [SerializeField] private bool avoidLedges = true;
    [Tooltip("How far below the leading foot corner to look for ground. If no ground is found within this distance, it counts as a ledge.")]
    [SerializeField] private float ledgeCheckDepth = 0.5f;
    [Tooltip("How far ahead of the leading foot corner the ledge probe is placed, in units.")]
    [SerializeField] private float ledgeCheckAhead = 0.05f;

    [Tooltip("Minimum seconds between direction flips, so a tight spot cannot cause flickering turns every physics tick.")]
    [SerializeField] private float turnCooldown = 0.2f;

    [Header("Visuals")]
    [Tooltip("Optional sprite to flip to match walk direction. Leave empty if an animator handles facing instead.")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("Enable if the sprite art faces left by default (matches the player art convention in this project).")]
    [SerializeField] private bool spriteFacesLeft = true;

    // Skin offset so rays start just inside the collider and cannot begin
    // embedded in an adjacent wall or the floor.
    private const float Skin = 0.02f;

    private CharacterMotor2D _motor;
    private Collider2D _collider;

    // -1 = walking left, +1 = walking right.
    private int _direction = 1;

    // Counts down after a flip; no new flip is allowed while > 0.
    private float _turnCooldownTimer;

    private void Awake()
    {
        _motor = GetComponent<CharacterMotor2D>();
        _collider = GetComponent<Collider2D>();

        // The motor moves this body purely through velocity writes, and a sleeping
        // Rigidbody2D ignores those. If the monster gets pinned for half a second
        // (e.g. caught on a tile seam), physics puts it to sleep and it would freeze
        // until an external collision wakes it. Never allow it to sleep.
        GetComponent<Rigidbody2D>().sleepMode = RigidbodySleepMode2D.NeverSleep;

        _direction = startDirection == StartDirection.Right ? 1 : -1;

        if (obstacleLayer == 0)
        {
            Debug.LogWarning($"{nameof(MonsterPatrolController)} on '{name}' has no Obstacle Layer assigned. It will never detect walls or ledges.", this);
        }
    }

    private void FixedUpdate()
    {
        _turnCooldownTimer -= Time.fixedDeltaTime;

        // Only re-evaluate direction while standing on ground. Checking mid-air
        // would make the monster flip while falling past a wall or off a ledge.
        if (_motor.IsGrounded && _turnCooldownTimer <= 0f && (IsWallAhead() || IsLedgeAhead()))
        {
            _direction = -_direction;
            _turnCooldownTimer = turnCooldown;
        }

        _motor.SetMoveInput(_direction);

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = spriteFacesLeft ? _direction > 0 : _direction < 0;
        }
    }

    /// <summary>
    /// Casts two short rays forward (foot height and mid height) from the leading
    /// edge of the collider. A hit with a mostly-horizontal surface normal counts
    /// as a wall; gentle slopes are walkable and do not trigger a turn.
    /// </summary>
    private bool IsWallAhead()
    {
        Bounds bounds = _collider.bounds;
        float frontX = _direction > 0 ? bounds.max.x : bounds.min.x;
        Vector2 dir = Vector2.right * _direction;
        float length = Skin + wallCheckDistance;

        Vector2 footOrigin = new Vector2(frontX - _direction * Skin, bounds.min.y + Skin);
        Vector2 midOrigin = new Vector2(frontX - _direction * Skin, bounds.center.y);

        return IsWallHit(Physics2D.Raycast(footOrigin, dir, length, obstacleLayer))
            || IsWallHit(Physics2D.Raycast(midOrigin, dir, length, obstacleLayer));
    }

    private static bool IsWallHit(RaycastHit2D hit)
    {
        return hit.collider != null && Mathf.Abs(hit.normal.x) > 0.5f;
    }

    /// <summary>
    /// Casts one ray downward from just past the leading bottom corner of the
    /// collider. If it finds no ground within <see cref="ledgeCheckDepth"/>,
    /// the floor is about to run out.
    /// </summary>
    private bool IsLedgeAhead()
    {
        if (!avoidLedges)
        {
            return false;
        }

        Vector2 origin = LedgeProbeOrigin();
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, Skin + ledgeCheckDepth, obstacleLayer);
        return hit.collider == null;
    }

    private Vector2 LedgeProbeOrigin()
    {
        Bounds bounds = _collider.bounds;
        float frontX = _direction > 0 ? bounds.max.x : bounds.min.x;
        return new Vector2(frontX + _direction * ledgeCheckAhead, bounds.min.y + Skin);
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D col = _collider != null ? _collider : GetComponent<Collider2D>();
        if (col == null)
        {
            return;
        }

        int dir = Application.isPlaying
            ? _direction
            : (startDirection == StartDirection.Right ? 1 : -1);

        Bounds bounds = col.bounds;
        float frontX = dir > 0 ? bounds.max.x : bounds.min.x;

        Gizmos.color = Color.red;
        Vector3 footOrigin = new Vector3(frontX, bounds.min.y + Skin);
        Vector3 midOrigin = new Vector3(frontX, bounds.center.y);
        Gizmos.DrawLine(footOrigin, footOrigin + Vector3.right * (dir * wallCheckDistance));
        Gizmos.DrawLine(midOrigin, midOrigin + Vector3.right * (dir * wallCheckDistance));

        if (avoidLedges)
        {
            Gizmos.color = Color.yellow;
            Vector3 ledgeOrigin = new Vector3(frontX + dir * ledgeCheckAhead, bounds.min.y + Skin);
            Gizmos.DrawLine(ledgeOrigin, ledgeOrigin + Vector3.down * ledgeCheckDepth);
        }
    }
}
