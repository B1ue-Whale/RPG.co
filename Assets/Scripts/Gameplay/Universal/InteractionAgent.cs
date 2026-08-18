using UnityEngine;

/// <summary>
/// Tracks the current <see cref="IInteractable"/> in range and lets it be triggered,
/// independent of who is doing the interacting. Interaction triggers (e.g.
/// <see cref="PipeTeleport"/>) register/unregister against whichever GameObject carries
/// this component, so both the player and an NPC can share the same interaction flow
/// without triggers needing to know about a specific controller type.
/// </summary>
public class InteractionAgent : MonoBehaviour
{
    private IInteractable _currentInteractable;

    /// <summary>
    /// Raised the instant <see cref="TryInteract"/> is called, regardless of whether an
    /// interactable was actually present. Lets external observers (e.g. a command
    /// recorder) capture the momentary interact press itself.
    /// </summary>
    public event System.Action Interacted;

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

    public void TryInteract()
    {
        Interacted?.Invoke();
        _currentInteractable?.Interact();
    }
}
