using UnityEngine;

/// <summary>
/// Drives a character Animator from <see cref="CharacterMotor2D"/> state.
/// Owns no logic of its own: it only mirrors motor state into Animator
/// parameters each frame and flips the sprite to match facing direction.
/// Used by both the player and NPCs. Supported Animator parameters:
/// "Speed" (float), "IsGrounded" (bool), "VerticalVelocity" (float), and "IsDead"
/// (bool, NPCs only - the player controller does not have that parameter).
///
/// A controller does not have to declare all of these. Whichever ones it is missing
/// are simply not written - e.g. Graphics_Monster only has "Speed", and writing the
/// others would log a "Parameter does not exist" warning every single frame, per
/// monster.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
    private static readonly int VerticalVelocityParam = Animator.StringToHash("VerticalVelocity");
    private static readonly int IsDeadParam = Animator.StringToHash("IsDead");
    private static readonly Color FreezeTint = new Color(1f, 0.5f, 0.08f, 1f);

    [Tooltip("Motor whose state is mirrored into the Animator.")]
    [SerializeField] private CharacterMotor2D motor;
    [Tooltip("Animator on the Graphics child.")]
    [SerializeField] private Animator animator;
    [Tooltip("Sprite renderer whose flipX is driven by facing direction.")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("Enable if the sprite art faces left by default (player art in this project). Disable for right-facing art such as the NPC.")]
    [SerializeField] private bool spriteFacesLeft = true;

    // Resolved once from the controller, since animator.parameters allocates.
    private bool _hasSpeed;
    private bool _hasIsGrounded;
    private bool _hasVerticalVelocity;
    private bool _hasIsDead;

    /// <summary>True while a death animation is playing. Cleared by <see cref="SetDead"/>.</summary>
    public bool IsDead { get; private set; }

    private bool _playbackFrozen;
    private Color _spriteColorBeforeFreeze;
    private bool _hasSpriteColorBeforeFreeze;

    /// <summary>
    /// Length of the Death_NPC clip if this animator has one, otherwise a fallback
    /// matching that clip's authored duration. Used so respawn can wait for the pose.
    /// </summary>
    public float DeathClipLength
    {
        get
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i] != null && clips[i].name == "Death_NPC")
                    {
                        return clips[i].length;
                    }
                }
            }

            return 0.875f;
        }
    }

    private void Awake()
    {
        CacheAvailableParameters();
    }

    private void CacheAvailableParameters()
    {
        _hasSpeed = false;
        _hasIsGrounded = false;
        _hasVerticalVelocity = false;
        _hasIsDead = false;

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            int nameHash = parameters[i].nameHash;

            if (nameHash == SpeedParam)
                _hasSpeed = true;
            else if (nameHash == IsGroundedParam)
                _hasIsGrounded = true;
            else if (nameHash == VerticalVelocityParam)
                _hasVerticalVelocity = true;
            else if (nameHash == IsDeadParam)
                _hasIsDead = true;
        }
    }

    /// <summary>
    /// Enters or leaves the death pose. While dead, motor state is no longer
    /// mirrored so walk/jump transitions cannot interrupt the clip.
    /// </summary>
    public void SetDead(bool dead)
    {
        IsDead = dead;
        if (dead)
        {
            SetPlaybackFrozen(false);
        }

        if (_hasIsDead)
        {
            animator.SetBool(IsDeadParam, dead);
        }
    }

    /// <summary>
    /// Holds the current animation pose (used by gadgets such as Garry's Gun).
    /// Motor state is not mirrored while frozen, so walk/idle cannot overwrite the pose.
    /// Applies an orange tint for the duration of the freeze.
    /// </summary>
    public void SetPlaybackFrozen(bool frozen)
    {
        _playbackFrozen = frozen;
        if (animator != null)
        {
            animator.speed = frozen ? 0f : 1f;
        }

        ApplyFreezeTint(frozen);
    }

    private void ApplyFreezeTint(bool frozen)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (frozen)
        {
            if (!_hasSpriteColorBeforeFreeze)
            {
                _spriteColorBeforeFreeze = spriteRenderer.color;
                _hasSpriteColorBeforeFreeze = true;
            }

            Color tint = FreezeTint;
            tint.a = _spriteColorBeforeFreeze.a;
            spriteRenderer.color = tint;
            return;
        }

        if (_hasSpriteColorBeforeFreeze)
        {
            spriteRenderer.color = _spriteColorBeforeFreeze;
            _hasSpriteColorBeforeFreeze = false;
        }
    }

    /// <summary>Shows or hides the character sprite without touching animator state.</summary>
    public void SetVisible(bool visible)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = visible;
        }
    }

    private void Update()
    {
        if (IsDead || _playbackFrozen)
        {
            return;
        }

        if (_hasSpeed)
            animator.SetFloat(SpeedParam, Mathf.Abs(motor.Velocity.x));
        if (_hasIsGrounded)
            animator.SetBool(IsGroundedParam, motor.IsGrounded);
        if (_hasVerticalVelocity)
            animator.SetFloat(VerticalVelocityParam, motor.Velocity.y);

        spriteRenderer.flipX = spriteFacesLeft
            ? motor.FacingDirection > 0
            : motor.FacingDirection < 0;
    }
}
