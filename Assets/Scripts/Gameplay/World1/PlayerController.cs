using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Translates player input into movement commands for <see cref="CharacterMotor2D"/>.
/// <para>
/// This component only reads input and forwards intentions. It never touches
/// physics directly, so the same motor can be reused by an NPC controller.
/// </para>
/// <para>
/// Designed to be driven by a <c>PlayerInput</c> component set to the
/// "Send Messages" behaviour. It automatically calls <see cref="OnMove(InputValue)"/>
/// and <see cref="OnJump(InputValue)"/> for the "Move" and "Jump" actions,
/// matched by name, so no manual event wiring is required.
/// </para>
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Tooltip("The character motor that will receive movement commands.")]
    [SerializeField] private CharacterMotor2D motor;

    // Last horizontal input read from the Move action, in [-1, 1].
    private float _horizontalInput;

    private void Awake()
    {
        if (motor == null)
        {
            Debug.LogError($"{nameof(PlayerController)} on '{name}' has no {nameof(CharacterMotor2D)} assigned.", this);
        }
    }

    private void Update()
    {
        // Continuously forward the stored horizontal input so the motor always
        // reflects the current stick/key state, even while a key is held.
        if (motor != null)
        {
            motor.SetMoveInput(_horizontalInput);
        }
    }

    /// <summary>
    /// Message from PlayerInput ("Send Messages" mode) for the "Move" action.
    /// Reads a Vector2 and keeps only the horizontal component.
    /// </summary>
    public void OnMove(InputValue value)
    {
        _horizontalInput = value.Get<Vector2>().x;
    }

    /// <summary>
    /// Message from PlayerInput ("Send Messages" mode) for the "Jump" action.
    /// Sends a single jump request on the press, ignoring the release.
    /// </summary>
    public void OnJump(InputValue value)
    {
        if (value.isPressed && motor != null)
        {
            motor.RequestJump();
        }
    }
}
