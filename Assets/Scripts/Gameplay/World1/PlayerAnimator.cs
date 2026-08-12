using UnityEngine;

/// <summary>
/// Drives the player's Animator from <see cref="CharacterMotor2D"/> state.
/// Owns no logic of its own: it only mirrors motor state into Animator
/// parameters each frame and flips the sprite to match facing direction.
/// Expected Animator parameters: "Speed" (float), "IsGrounded" (bool),
/// "VerticalVelocity" (float).
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
    [Tooltip("Sprite renderer to flip when facing left.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private void Update()
    {
        animator.SetFloat(SpeedParam, Mathf.Abs(motor.Velocity.x));
        animator.SetBool(IsGroundedParam, motor.IsGrounded);
        animator.SetFloat(VerticalVelocityParam, motor.Velocity.y);

        // Sprite art faces left by default, so flip when moving right.
        spriteRenderer.flipX = motor.FacingDirection > 0;
    }
}
