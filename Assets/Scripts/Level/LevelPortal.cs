using UnityEngine;
using UnityEngine.InputSystem;

public class LevelPortal : MonoBehaviour, IInteractable
{
    [SerializeField]
    private string targetSceneName;


    public void Interact()
    {
        LevelTransition.EnterLevel(targetSceneName);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            var controller = other.GetComponent<PlayerController>();
            controller?.SetInteractable(this);
        }
    }

    private void OnTriggerExit2D(Collider2D other) //stop touching 
    {
        if (other.CompareTag("Player"))
        {
            var controller = other.GetComponent<PlayerController>();
            controller?.ClearInteractable(this);
        }
    }
}