using System.Collections.Generic;
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
    [Tooltip("Tracks the current interactable in range and triggers it. Shared with NPC playback so interaction triggers don't need to know about PlayerController specifically.")]
    [SerializeField] private InteractionAgent interactionAgent;
    [SerializeField] private PlayerHideController playerHideController;
    [Tooltip("Bug zone searched for the nearest infected tile when E is pressed alone (no directional input needed). Found automatically if left unassigned.")]
    [SerializeField] private BugZone bugZone;
    [Tooltip("World-space radius (in tiles' worth of distance) within which E auto-targets the nearest infected tile. ~2 tiles.")]
    [SerializeField] private float bugSnapRange = 2f;

    // Last horizontal input read from the Move action, in [-1, 1].
    private float _horizontalInput;

    private void Awake()
    {
        if (motor == null)
        {
            Debug.LogError($"{nameof(PlayerController)} on '{name}' has no {nameof(CharacterMotor2D)} assigned.", this);
        }

        if (bugZone == null)
        {
            bugZone = FindFirstObjectByType<BugZone>();
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
            // Temporarily disabled: attacking makes the player disappear (no
            // attack animation yet). Re-enable once that's sorted out.
            // combat.Attack();
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

        // E alone auto-targets the nearest infected tile within bugSnapRange - no
        // directional input needed. Only triggers this path when a valid tile is
        // actually in range; otherwise falls through to the interactions below.
        if (playerHideController != null && TryFindNearestInfectedCell(out Vector3Int bugCell))
        {
            Debug.Log("E버튼 눌림 (근처 버그 자동 타겟)");
            playerHideController.EnterHideAt(bugCell);
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
        if (interactionAgent != null)
        {
            Debug.Log("E버튼 눌림");
            interactionAgent.TryInteract();
        }
    }

    /// <summary>
    /// Nearest currently-infected tile within bugSnapRange of the player, by world-space
    /// distance. Only considers tiles the hide system would actually accept, so this
    /// stays in lockstep with whatever EnterHideAt() requires. No chaining: this is
    /// evaluated fresh on each E press, never automatically re-triggered.
    /// </summary>
    private bool TryFindNearestInfectedCell(out Vector3Int nearestCell)
    {
        nearestCell = default;

        if (bugZone == null)
        {
            return false;
        }

        IReadOnlyList<Vector3Int> infectedCells = bugZone.InfectedCells;
        if (infectedCells.Count == 0)
        {
            return false;
        }

        Vector3 playerPosition = transform.position;
        float bestSqrDistance = bugSnapRange * bugSnapRange;
        bool found = false;

        for (int i = 0; i < infectedCells.Count; i++)
        {
            Vector3Int cell = infectedCells[i];
            if (!playerHideController.CanHideAt(cell))
            {
                continue;
            }

            float sqrDistance = (bugZone.GetCellWorldCenter(cell) - playerPosition).sqrMagnitude;
            if (sqrDistance <= bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                nearestCell = cell;
                found = true;
            }
        }

        return found;
    }
}
