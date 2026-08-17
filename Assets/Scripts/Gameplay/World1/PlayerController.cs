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
    [SerializeField] private PlayerCombat combat;
    private IInteractable _currentInteractable;
    [SerializeField] private PlayerHideController playerHideController;

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
        if (playerHideController != null && playerHideController.IsHidden)
        {
            _horizontalInput = 0f; 
            return;
        }
        _horizontalInput = value.Get<Vector2>().x;
    }

    /// <summary>
    /// Message from PlayerInput ("Send Messages" mode) for the "Jump" action.
    /// Press requests a jump; hold/release is forwarded so jump height can vary.
    /// </summary>
    public void OnJump(InputValue value)
    {
        if (motor == null)
        {
            return;
        }

        bool pressed = value.isPressed;
        if (playerHideController != null && playerHideController.IsHidden)
        {
            motor.SetJumpHeld(false);
            return;
        }

        if (pressed)
        {
            motor.RequestJump();
        }

        motor.SetJumpHeld(pressed);
    }
    public void OnAttack(InputValue value)
    {
        if (!value.isPressed)
            return;

        // Block attack while hidden
        if (playerHideController != null && playerHideController.IsHidden)
        {
            Debug.Log("공격 차단: 숨은 상태");
            return;
        }

        if (combat != null)
        {
            Debug.Log("공격");
            combat.Attack();
        }
    }
    public void OnInteract(InputValue value)
    {
        if (!value.isPressed)
            return;
        // If player is currently hidden, pressing E only exits hide state. Block other interactions while hidden.
        if (playerHideController != null && playerHideController.IsHidden)
        {
            playerHideController.ExitHide();
            return;
        }
        if ((Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
        && playerHideController != null)
        {
            Debug.Log("숨기버튼 눌림");
            if (playerHideController.IsHidden)
            {
                playerHideController.ExitHide();
                return;
            }
        
            if (playerHideController.CanHideHere())
            {
                playerHideController.EnterHide();
                return;
            }
        }
        if (_currentInteractable != null)
        {
            Debug.Log("E버튼 눌림");
            _currentInteractable.Interact();
        }
    }
    
    public void SetInteractable(IInteractable interactable)
    {
        _currentInteractable = interactable;
    }
    public void ClearInteractable(IInteractable interactable)
    {
        if (_currentInteractable == interactable)
        {
            _currentInteractable = null;
        }
    }
}
