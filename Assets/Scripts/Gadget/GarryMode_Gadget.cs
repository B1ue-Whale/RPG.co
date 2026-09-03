using System.Collections;
using UnityEngine;

public class GarryMode_Gadget : GadgetBase
{
    //게리건(garrys mod) - npc 잠시 정지
    [SerializeField] private float range = 5f;
    [SerializeField] private float duration = 3f;
    [SerializeField] private Transform player;

    private void Awake()
    {
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
        NpcCommandPlayback target = FindNearestNpcInRange();
        if (target == null)
        {
            Debug.LogWarning("범위 안에 멈출 NPC가 없습니다.");
            return false;
        }

        StartCoroutine(FreezeNpc(target, duration));
        Debug.Log("Garrys gun Used!");
        return true;
    }

    private IEnumerator FreezeNpc(NpcCommandPlayback target, float freezeDuration)
    {
        target.ForceFreeze();
        yield return new WaitForSeconds(freezeDuration);
        if (target != null)
        {
            target.ForceUnfreeze();
        }
    }

    private NpcCommandPlayback FindNearestNpcInRange()
    {
        NpcCommandPlayback[] npcs = FindObjectsByType<NpcCommandPlayback>(FindObjectsInactive.Exclude);
        NpcCommandPlayback nearest = null;
        float nearestSqrDistance = range * range;
        Vector3 origin = player != null ? player.position : transform.position;

        for (int i = 0; i < npcs.Length; i++)
        {
            NpcCommandPlayback npc = npcs[i];
            if (npc == null)
            {
                continue;
            }

            float sqrDistance = (npc.transform.position - origin).sqrMagnitude;
            if (sqrDistance <= nearestSqrDistance)
            {
                nearest = npc;
                nearestSqrDistance = sqrDistance;
            }
        }

        return nearest;
    }
}
