using UnityEngine;

/// <summary>
/// Drives a character Animator from <see cref="CharacterMotor2D"/> state.
/// Owns no logic of its own: it only mirrors motor state into Animator
/// parameters each frame and flips the sprite to match facing direction.
/// Used by both the player and NPCs. Supported Animator parameters:
/// "Speed" (float), "IsGrounded" (bool), "VerticalVelocity" (float).
///
/// A controller does not have to declare all three. Whichever ones it is missing are
/// simply not written - e.g. Graphics_Monster only has "Speed", and writing the other
/// two would log a "Parameter does not exist" warning every single frame, per monster.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
    private static readonly int VerticalVelocityParam = Animator.StringToHash("VerticalVelocity");

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

    private void Awake()
    {
        CacheAvailableParameters();
    }

    private void CacheAvailableParameters()
    {
        _hasSpeed = false;
        _hasIsGrounded = false;
        _hasVerticalVelocity = false;

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
        }
    }

    private void Update()
    {
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
