using UnityEngine;

public class PipeTeleport : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform exitPoint;

    private PlayerController _playerController;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        _playerController = other.GetComponent<PlayerController>();
        _playerController?.SetInteractable(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        var controller = other.GetComponent<PlayerController>();
        controller?.ClearInteractable(this);

        if (controller == _playerController)
            _playerController = null;
    }

    public void Interact()
    {
        if (_playerController == null || exitPoint == null)
            return;

        _playerController.transform.position = exitPoint.position;
    }
}