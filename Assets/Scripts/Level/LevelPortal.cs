using UnityEngine;

public class LevelPortal : MonoBehaviour, IInteractable
{
    [SerializeField]
    private string targetSceneName;

    //구본환 8.16 클리어 전까지 포탈 잠금
    [SerializeField]
    private bool startsLocked = true;
    private bool unlocked;
    private PlayerController nearbyPlayer;

    private void Awake()
    {
        unlocked = !startsLocked;
    }

    //구본환 8.16
    public void SetUnlocked(bool value)
    {
        unlocked = value;

        if (unlocked && nearbyPlayer != null)
            nearbyPlayer.SetInteractable(this);
        else if (!unlocked && nearbyPlayer != null)
            nearbyPlayer.ClearInteractable(this);
    }

    public void Interact()
    {
        if (!unlocked) //구본환 8.16
            return;

        LevelTransition.EnterLevel(targetSceneName);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        //구본환 8.16 잠금 해제된 경우에만 상호작용 등록
        nearbyPlayer = other.GetComponent<PlayerController>();
        if (unlocked)
            nearbyPlayer?.SetInteractable(this);
    }

    private void OnTriggerExit2D(Collider2D other) //stop touching
    {
        if (!other.CompareTag("Player"))
            return;

        var controller = other.GetComponent<PlayerController>();
        controller?.ClearInteractable(this);

        //구본환 8.16
        if (controller == nearbyPlayer)
            nearbyPlayer = null;
    }
}
