using UnityEngine;

/// <summary>
/// Drives a character Animator from <see cref="CharacterMotor2D"/> state.
/// Owns no logic of its own: it only mirrors motor state into Animator
/// parameters each frame and flips the sprite to match facing direction.
/// Used by both the player and NPCs. Expected Animator parameters:
/// "Speed" (float), "IsGrounded" (bool), "VerticalVelocity" (float).
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

    private void Update()
    {
        animator.SetFloat(SpeedParam, Mathf.Abs(motor.Velocity.x));
        animator.SetBool(IsGroundedParam, motor.IsGrounded);
        animator.SetFloat(VerticalVelocityParam, motor.Velocity.y);

        spriteRenderer.flipX = spriteFacesLeft
            ? motor.FacingDirection > 0
            : motor.FacingDirection < 0;
    }
}
