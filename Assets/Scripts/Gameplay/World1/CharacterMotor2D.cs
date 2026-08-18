using UnityEngine;

/// Reusable 2D character movement logic driven purely by movement intentions.

/// This component knows nothing about input devices, AI, or who is controlling it.
/// A player controller or an NPC controller can both feed it commands through
/// <see cref="SetMoveInput(float)"/>, <see cref="RequestJump"/>, and
/// <see cref="SetJumpHeld(bool)"/>.

[RequireComponent(typeof(Rigidbody2D))]
public class CharacterMotor2D : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Horizontal movement speed in units per second.")]
    [SerializeField] private float moveSpeed = 6f;
    [Tooltip("Seconds of run-up from a standstill to full speed while holding a direction. 0 = instant.")]
    [SerializeField] private float accelerationTime = 0.35f;
    [Tooltip("Seconds to stop from max speed when there is no input. Keep this short so the character does not feel icy. 0 = instant.")]
    [SerializeField] private float decelerationTime = 0.05f;
    [Tooltip("Seconds to brake from max speed when holding the opposite direction. After stopping, a new run-up begins. 0 = instant.")]
    [SerializeField] private float turnaroundTime = 0.08f;

    [Header("Jump")]
    [Tooltip("Upward velocity applied when a jump is executed. Hold the jump button to keep this full height.")]
    [SerializeField] private float jumpForce = 12f;
    [Tooltip("Gravity multiplier while falling. Higher values make the descent snappier.")]
    [SerializeField] private float fallGravityMultiplier = 1.6f;
    [Tooltip("Gravity multiplier while rising after the jump button is released. Higher values make tap jumps shorter.")]
    [SerializeField] private float jumpCutGravityMultiplier = 2.5f;
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

    // How long the current direction has been held. Drives the run-up to full speed.
    private float _accelTimer;

    // Counts down from jumpBufferTime after a jump request, so a press made
    // slightly before landing still fires. Jump is buffered while > 0.
    private float _jumpBufferCounter;

    // Counts down from coyoteTime after leaving the ground, so a jump is still
    // allowed for a short grace period after walking off a ledge. > 0 = jumpable.
    private float _coyoteTimeCounter;

    // True while the controller is holding jump. RequestJump sets this; SetJumpHeld(false) clears it.
    private bool _jumpHeld;

    // True from the moment a jump is executed until upward speed runs out.
    // Used so only our own jumps get the early-release gravity cut.
    private bool _isJumping;

    // Gravity scale authored on the Rigidbody2D. Multipliers are applied on top of this.
    private float _baseGravityScale = 1f;

    // -1 = facing left, +1 = facing right. Defaults to right.
    private int _facing = 1;

    /// <summary>True while the ground-check box overlaps a ground collider.</summary>
    public bool IsGrounded { get; private set; }

    /// <summary>Current rigidbody velocity (read-only mirror).</summary>
    public Vector2 Velocity => _rigidbody != null ? _rigidbody.linearVelocity : Vector2.zero;

    /// <summary>-1 when facing left, +1 when facing right.</summary>
    public int FacingDirection => _facing;

    /// <summary>The move input currently pending consumption by this tick's FixedUpdate.</summary>
    public float PendingMoveInput => _moveInput;

    /// <summary>
    /// Raised the instant <see cref="RequestJump"/> is called, before buffering/coyote
    /// logic runs. Lets external observers (e.g. a command recorder) capture the
    /// momentary jump press itself rather than inferring it from buffer/airborne state.
    /// </summary>
    public event System.Action JumpRequested;

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
            _baseGravityScale = _rigidbody.gravityScale;
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
        JumpRequested?.Invoke();
        _jumpBufferCounter = jumpBufferTime;
        _jumpHeld = true;
    }

    /// <summary>
    /// Tells the motor whether jump is currently held. Release this while still
    /// rising to cut the jump short, 
    /// </summary>
    public void SetJumpHeld(bool held)
    {
        _jumpHeld = held;
    }

    /// <summary>
    /// Clears pending move input and the jump buffer/coyote timers, without touching
    /// position or velocity. Used when a character is snapped to a checkpoint so it
    /// doesn't carry over stale intent (e.g. a jump buffered a tick before teleporting).
    /// </summary>
    public void ResetTransientState()
    {
        _moveInput = 0f;
        _jumpBufferCounter = 0f;
        _coyoteTimeCounter = 0f;
    }

    /// <summary>
    /// Directly sets facing direction (left/right) without touching velocity or move
    /// input. Facing is normally only derived from move input sign via
    /// <see cref="ApplyHorizontalMovement"/>; this lets external logic (e.g. Suspicious
    /// behavior) turn a stationary character to look at something.
    /// </summary>
    public void SetFacing(int direction)
    {
        if (direction > 0)
        {
            _facing = 1;
        }
        else if (direction < 0)
        {
            _facing = -1;
        }
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
        ApplyJumpGravity();
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

        if (Mathf.Abs(_moveInput) < 0.01f)
        {
            // No input: brake quickly so releasing a direction does not slide.
            _accelTimer = 0f;
            velocity.x = MoveTowardsSpeed(velocity.x, 0f, decelerationTime);
        }
        else if (Mathf.Abs(velocity.x) > 0.01f && velocity.x * _moveInput < 0f)
        {
            // Opposite input: kill leftover speed, then a fresh run-up can start.
            _accelTimer = 0f;
            velocity.x = MoveTowardsSpeed(velocity.x, 0f, turnaroundTime);
        }
        else
        {
            // Held direction: speed follows a run-up from 0 to full over accelerationTime.
            _accelTimer += Time.fixedDeltaTime;
            float startup = accelerationTime <= 0f
                ? 1f
                : Mathf.Clamp01(_accelTimer / accelerationTime);
            velocity.x = _moveInput * moveSpeed * startup;
        }

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

    /// <summary>
    /// Moves <paramref name="current"/> toward <paramref name="target"/> so that
    /// covering <see cref="moveSpeed"/> takes <paramref name="time"/> seconds.
    /// A time of 0 or less is treated as instant.
    /// </summary>
    private float MoveTowardsSpeed(float current, float target, float time)
    {
        float maxDelta = time <= 0f
            ? float.PositiveInfinity
            : (moveSpeed / time) * Time.fixedDeltaTime;

        return Mathf.MoveTowards(current, target, maxDelta);
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

        _isJumping = true;

        Vector2 velocity = _rigidbody.linearVelocity;
        velocity.y = jumpForce;
        _rigidbody.linearVelocity = velocity;
    }

    /// <summary>
    /// Scales gravity so a held jump keeps the full arc, an early release falls
    /// short, and descending is a bit snappier than the rise.
    /// </summary>
    private void ApplyJumpGravity()
    {
        float multiplier = 1f;
        float verticalSpeed = _rigidbody.linearVelocity.y;

        if (verticalSpeed < -0.01f)
        {
            multiplier = fallGravityMultiplier;
            _isJumping = false;
        }
        else if (_isJumping && verticalSpeed > 0f && !_jumpHeld)
        {
            multiplier = jumpCutGravityMultiplier;
        }

        _rigidbody.gravityScale = _baseGravityScale * multiplier;
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
