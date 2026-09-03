using UnityEngine;

public class FPS_Gadget : GadgetBase
{//FPS : 현대식 수류탄 - 근처에 있는 버그블럭 바로 삭제(NPC 경계도 크게 증가) 
    [SerializeField]
    private float range = 3f;

    [SerializeField]
    private BugZone bugZone;

    [SerializeField]
    private Transform player;

    private void Awake()
    {
        if (bugZone == null)
        {
            bugZone = FindAnyObjectByType<BugZone>();
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }
    }

    protected override bool Use()
    {
        if (bugZone == null)
        {
            Debug.LogWarning("BugZone이 연결되지 않았습니다.");
            return false;
        }

        Vector3 center = player != null ? player.position : transform.position;
        Debug.Log("FPS Used!");
        // Deliberate player action, same as hand-cleaning: arms the recently-cleaned
        // cooldown on every tile it removes, and can earn Relief if it empties the NPC area.
        bugZone.ClearInfectionInRange(center, range, InfectionClearCause.Player);
        return true;
    }
}
