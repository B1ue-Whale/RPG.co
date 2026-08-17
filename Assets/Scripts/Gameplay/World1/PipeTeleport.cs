using UnityEngine;

public class PipeTeleport : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform exitPoint;

    private InteractionAgent _agent;
    private Transform _interactorTransform;

    private void OnTriggerEnter2D(Collider2D other)
    {
        var agent = other.GetComponent<InteractionAgent>();
        if (agent == null)
            return;

        _agent = agent;
        _interactorTransform = other.transform;
        agent.SetInteractable(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var agent = other.GetComponent<InteractionAgent>();
        if (agent == null)
            return;

        agent.ClearInteractable(this);

        if (agent == _agent)
        {
            _agent = null;
            _interactorTransform = null;
        }
    }

    public void Interact()
    {
        if (_interactorTransform == null || exitPoint == null)
            return;

        _interactorTransform.position = exitPoint.position;
    }
}