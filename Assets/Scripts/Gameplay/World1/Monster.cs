using UnityEngine;

/// Walking monster: patrols left/right, and resolves contact with an NPC as either
/// a stomp (NPC lands on top - kills the monster) or a side hit (kills the NPC, same
/// reset as KillBorder). Feeds move input into the shared <see cref="CharacterMotor2D"/>
/// so movement matches other characters. Turns around at walls or ledges.
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

    [Header("Kill")]
    [Tooltip("Minimum absolute X of a contact normal to count as a side hit. 0.5 matches the wall-check (mostly-horizontal). Lower = more forgiving side detection.")]
    [SerializeField] private float minSideNormal = 0.5f;
    [Tooltip("Minimum downward speed (units/second) of the NPC relative to this monster, at the moment of contact, to count as a legitimate stomp from above. Filters out near-zero velocity noise from a frictionless body settling into resting contact - a real jump/fall onto the monster clears this by a wide margin.")]
    [SerializeField] private float stompMinDownwardSpeed = 0.5f;
    [Tooltip("How far below this monster's vertical midpoint the NPC's lowest point is still allowed to be and count as a stomp, in units. Covers the case where physics resolves the landing a bit lower than the NPC's true contact moment. Still requires stompMinDownwardSpeed, so it does not turn an ordinary side hit into a stomp.")]
    [SerializeField] private float stompVerticalForgiveness = 0.1f;

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
    private Rigidbody2D _rigidbody;

    // -1 = walking left, +1 = walking right.
    private int _direction = 1;

    // Counts down after a flip; no new flip is allowed while > 0.
    private float _turnCooldownTimer;

    private bool _isDead;
    private Vector2 _spawnPosition;
    private int _spawnDirection;

    /// <summary>True once this monster has been stomped. Cleared by ResetMonster().</summary>
    public bool IsDead => _isDead;

    private void Awake()
    {
        _motor = GetComponent<CharacterMotor2D>();
        _collider = GetComponent<Collider2D>();
        _rigidbody = GetComponent<Rigidbody2D>();

        // The motor moves this body purely through velocity writes, and a sleeping
        // Rigidbody2D ignores those. If the monster gets pinned for half a second
        // (e.g. caught on a tile seam), physics puts it to sleep and it would freeze
        // until an external collision wakes it. Never allow it to sleep.
        _rigidbody.sleepMode = RigidbodySleepMode2D.NeverSleep;

        _spawnDirection = startDirection == StartDirection.Right ? 1 : -1;
        _direction = _spawnDirection;
        _spawnPosition = _rigidbody.position;

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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryKillFromCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryKillFromCollision(collision);
    }

    private void TryKillFromCollision(Collision2D collision)
    {
        NpcProgressionController npc = ResolveNpc(collision);
        if (npc == null || npc.IsDying)
        {
            // Ordinary ground/wall contact, or the NPC is already in its death clip.
            return;
        }

        if (IsStomp(collision))
        {
            Die();
            return;
        }

        if (IsSideContact(collision))
        {
            npc.Die();
        }
    }

    /// <summary>
    /// Kills this monster (e.g. an NPC stomped it from above): disables the
    /// GameObject so it stops moving, colliding, and rendering. No impulse or
    /// velocity change is applied to whatever killed it.
    /// </summary>
    public void Die()
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Reverts a stomped monster back to its spawn position/direction and
    /// reactivates it. SetActive(true) does not re-run Awake(), so every piece of
    /// spawn-derived state that Awake() would have set up is restored explicitly here.
    /// </summary>
    public void ResetMonster()
    {
        if (!_isDead)
        {
            return;
        }

        _isDead = false;
        gameObject.SetActive(true);

        ProgressCheckpoint.TeleportRigidbody(_rigidbody, _spawnPosition);
        _direction = _spawnDirection;
        _turnCooldownTimer = 0f;
    }

    /// <summary>
    /// A legitimate stomp: the NPC's lowest point is at/above this monster's vertical
    /// midpoint, allowing stompVerticalForgiveness units of slack for physics
    /// resolving the landing slightly lower than the NPC's true contact moment
    /// (regardless of what the capsule's curved contact normal happens to report),
    /// and it is moving downward relative to this monster faster than
    /// stompMinDownwardSpeed - that speed requirement is what keeps the forgiveness
    /// from turning an ordinary side hit into a stomp. Deliberately does not require
    /// vertical-dominant velocity - a fast horizontal run should not disqualify an
    /// otherwise clear landing from above.
    /// </summary>
    private bool IsStomp(Collision2D collision)
    {
        Collider2D npcCollider = collision.collider;
        Rigidbody2D npcBody = collision.rigidbody;
        if (npcCollider == null || npcBody == null)
        {
            return false;
        }

        bool npcAboveMonster = npcCollider.bounds.min.y >= _collider.bounds.center.y - stompVerticalForgiveness;
        if (!npcAboveMonster)
        {
            return false;
        }

        float relativeVerticalVelocity = npcBody.linearVelocity.y - _rigidbody.linearVelocity.y;
        return relativeVerticalVelocity < -stompMinDownwardSpeed;
    }

    private static NpcProgressionController ResolveNpc(Collision2D collision)
    {
        // Resolve through the rigidbody so child colliders on the NPC still find the
        // controller on the root object (same pattern as KillBorder).
        if (collision.rigidbody != null)
        {
            NpcProgressionController fromBody = collision.rigidbody.GetComponentInParent<NpcProgressionController>();
            if (fromBody != null)
            {
                return fromBody;
            }
        }

        return collision.collider != null
            ? collision.collider.GetComponentInParent<NpcProgressionController>()
            : null;
    }

    private bool IsSideContact(Collision2D collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            if (Mathf.Abs(normal.x) > minSideNormal)
            {
                return true;
            }
        }

        return false;
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
