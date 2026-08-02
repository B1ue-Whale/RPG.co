using UnityEngine;
using UnityEngine.InputSystem;

public class LevelPortal : MonoBehaviour
{
    [SerializeField]
    private string targetSceneName;

    private bool playerIsNearby;

    private void Update()
    {
        if (playerIsNearby && Keyboard.current.eKey.isPressed)
        {
            LevelTransition.EnterLevel(targetSceneName);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsNearby = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other) //stop touching 
    {
        if (other.CompareTag("Player"))
        {
            playerIsNearby = false;
        }
    }
}