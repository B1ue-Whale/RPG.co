using UnityEngine;

/// <summary>
/// Reusable 2D character movement logic driven purely by movement intentions.
/// <para>
/// This component knows nothing about input devices, AI, or who is controlling it.
/// A player controller or an NPC controller can both feed it commands through
/// <see cref="SetMoveInput(float)"/> and <see cref="RequestJump"/>.
/// </para>
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class CharacterMotor2D : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Horizontal movement speed in units per second.")]
    [SerializeField] private float moveSpeed = 6f;

    [Header("Jump")]
    [Tooltip("Upward impulse applied when a jump is executed.")]
    [SerializeField] private float jumpForce = 12f;
    [Tooltip("Grace period after leaving the ground during which a jump is still allowed (coyote time), in seconds.")]
    [SerializeField] private float coyoteTime = 0.1f;
    [Tooltip("How long a jump request is remembered before landing so it fires on touchdown (jump buffering), in seconds.")]
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("Ground Check")]
    [Tooltip("Transform marking the point where the ground overlap box is centered.")]
    [SerializeField] private Transform groundCheck;
    [Tooltip("Size of the box used to detect the ground.")]
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.4f, 0.1f);
    [Tooltip("Layers that count as ground.")]
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D _rigidbody;

    // Current movement intention, clamped to [-1, 1].
    private float _moveInput;

    // Counts down from jumpBufferTime after a jump request, so a press made
    // slightly before landing still fires. Jump is buffered while > 0.
    private float _jumpBufferCounter;

    // Counts down from coyoteTime after leaving the ground, so a jump is still
    // allowed for a short grace period after walking off a ledge. > 0 = jumpable.
    private float _coyoteTimeCounter;

    // -1 = facing left, +1 = facing right. Defaults to right.
    private int _facing = 1;

    /// <summary>True while the ground-check box overlaps a ground collider.</summary>
    public bool IsGrounded { get; private set; }

    /// <summary>Current rigidbody velocity (read-only mirror).</summary>
    public Vector2 Velocity => _rigidbody != null ? _rigidbody.linearVelocity : Vector2.zero;

    /// <summary>-1 when facing left, +1 when facing right.</summary>
    public int FacingDirection => _facing;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();

        if (_rigidbody == null)
        {
            Debug.LogError($"{nameof(CharacterMotor2D)} on '{name}' requires a Rigidbody2D.", this);
        }
        else
        {
            // A frictionless body cannot cling to walls: pushing into a wall gives no
            // vertical grip, so gravity always drags the character back down. Applied
            // in code so it can't be lost to an unset inspector field.
            _rigidbody.sharedMaterial = new PhysicsMaterial2D("CharacterNoFriction")
            {
                friction = 0f,
                bounciness = 0f
            };
        }

        if (groundCheck == null)
        {
            Debug.LogError($"{nameof(CharacterMotor2D)} on '{name}' is missing its Ground Check transform. Ground detection will not work.", this);
        }

        if (groundLayer == 0)
        {
            Debug.LogWarning($"{nameof(CharacterMotor2D)} on '{name}' has no Ground Layer assigned. The character will never be considered grounded.", this);
        }
    }

    /// <summary>
    /// Sets the desired horizontal movement. Input is clamped to [-1, 1].
    /// </summary>
    public void SetMoveInput(float input)
    {
        _moveInput = Mathf.Clamp(input, -1f, 1f);
    }

    /// <summary>
    /// Requests a single jump. The request is buffered for <c>jumpBufferTime</c>
    /// seconds, so a press made slightly before landing still fires on touchdown.
    /// Calling this repeatedly only refreshes the buffer; it cannot stack jumps.
    /// </summary>
    public void RequestJump()
    {
        _jumpBufferCounter = jumpBufferTime;
    }

    private void FixedUpdate()
    {
        if (_rigidbody == null)
        {
            return;
        }

        UpdateGrounded();
        UpdateJumpTimers();
        ApplyHorizontalMovement();
        TryConsumeJump();
    }

    private void UpdateGrounded()
    {
        if (groundCheck == null)
        {
            IsGrounded = false;
            return;
        }

        // Fire a few thin downward rays spread across the foot width. Rays have no
        // width (unlike a box cast), so as long as they stay inside the body's
        // footprint they never catch an adjacent wall the way a box's side edge does
        // - which is what stopped jumps next to walls. Requiring a mostly-upward
        // surface normal still keeps walls and steep surfaces from counting as ground.
        const float skin = 0.05f;
        float halfWidth = groundCheckSize.x * 0.5f;
        float rayLength = groundCheckSize.y + skin;
        Vector2 feet = (Vector2)groundCheck.position + Vector2.up * skin;

        IsGrounded = false;
        for (int i = -1; i <= 1; i++)
        {
            Vector2 rayOrigin = feet + Vector2.right * (halfWidth * i);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, rayLength, groundLayer);
            if (hit.collider != null && hit.normal.y > 0.5f)
            {
                IsGrounded = true;
                break;
            }
        }
    }

    private void UpdateJumpTimers()
    {
        // Refresh coyote time while grounded; otherwise let it tick down so a jump
        // stays available for a short grace period after walking off a ledge.
        if (IsGrounded)
        {
            _coyoteTimeCounter = coyoteTime;
        }
        else
        {
            _coyoteTimeCounter -= Time.fixedDeltaTime;
        }

        // Let the buffered jump request expire so it does not fire much later.
        _jumpBufferCounter -= Time.fixedDeltaTime;
    }

    private void ApplyHorizontalMovement()
    {
        // Preserve vertical velocity; only drive the horizontal component.
        Vector2 velocity = _rigidbody.linearVelocity;
        velocity.x = _moveInput * moveSpeed;
        _rigidbody.linearVelocity = velocity;

        if (_moveInput > 0.01f)
        {
            _facing = 1;
        }
        else if (_moveInput < -0.01f)
        {
            _facing = -1;
        }
    }

    private void TryConsumeJump()
    {
        // Jump fires only when a request is still buffered and we are within the
        // coyote-time window. Either timer lapsing cancels it.
        if (_jumpBufferCounter <= 0f || _coyoteTimeCounter <= 0f)
        {
            return;
        }

        // Clear both timers so the request cannot fire again and no second jump
        // sneaks through the remaining coyote window.
        _jumpBufferCounter = 0f;
        _coyoteTimeCounter = 0f;

        Vector2 velocity = _rigidbody.linearVelocity;
        velocity.y = jumpForce;
        _rigidbody.linearVelocity = velocity;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = Application.isPlaying && IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
    }
}
