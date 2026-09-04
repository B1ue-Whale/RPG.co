using System.Collections;
using UnityEngine;

public class GarryMode_Gadget : GadgetBase
{
    //게리건(garrys mod) - npc 잠시 정지
    [SerializeField] private float range = 5f;
    [SerializeField] private float duration = 3f;
    [SerializeField] private Transform player;

    private Coroutine freezeRoutine;
    private NpcCommandPlayback frozenNpc;
    private PlayerAnimator frozenVisual;

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

        if (freezeRoutine != null)
        {
            StopCoroutine(freezeRoutine);
            ReleaseFreeze();
        }

        freezeRoutine = StartCoroutine(FreezeNpc(target, duration));
        Debug.Log("Garrys gun Used!");
        return true;
    }

    private IEnumerator FreezeNpc(NpcCommandPlayback target, float freezeDuration)
    {
        ApplyFreeze(target);
        yield return new WaitForSeconds(freezeDuration);
        if (frozenNpc == target)
        {
            ReleaseFreeze();
        }

        freezeRoutine = null;
    }

    private void ApplyFreeze(NpcCommandPlayback target)
    {
        frozenNpc = target;
        frozenVisual = target.GetComponent<PlayerAnimator>();
        target.GetComponent<NpcMonsterJumpReaction>()?.Cancel();
        frozenVisual?.SetPlaybackFrozen(true);
        target.ForceFreeze();
    }

    private void ReleaseFreeze()
    {
        if (frozenVisual != null)
        {
            frozenVisual.SetPlaybackFrozen(false);
        }

        NpcProgressionController progression = frozenNpc != null
            ? frozenNpc.GetComponent<NpcProgressionController>()
            : null;
        if (frozenNpc != null && (progression == null || !progression.IsDying))
        {
            frozenNpc.ForceUnfreeze();
        }

        frozenNpc = null;
        frozenVisual = null;
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
