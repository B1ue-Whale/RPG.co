using UnityEngine;

public class GarryMode_Gadget : GadgetBase
{
    //게리건(garrys mod) - npc 잠시 정지
    [SerializeField] private StatusEffectController statusEffectController;
    [SerializeField] private float range = 5f;
    [SerializeField] private float duration = 3f;
    
    protected override bool Use()
    {
        StatusEffect freeze = new StatusEffect(StatusEffectType.Freeze, 0f, duration);
        if (statusEffectController != null)
        {
            statusEffectController.ApplyEffect(freeze);
            Debug.Log("Garrys gun Used!");
            return true;
        }

        NpcCommandPlayback target = FindNearestNpcInRange();
        if (target == null)
        {
            Debug.LogWarning("범위 안에 멈출 NPC가 없습니다.");
            return false;
        }

        target.ApplyStatusEffect(freeze);
        Debug.Log("Garrys gun Used!");
        return true;
    }

    private NpcCommandPlayback FindNearestNpcInRange()
    {
        NpcCommandPlayback[] npcs = FindObjectsByType<NpcCommandPlayback>(FindObjectsInactive.Exclude);
        NpcCommandPlayback nearest = null;
        float nearestSqrDistance = range * range;

        for (int i = 0; i < npcs.Length; i++)
        {
            NpcCommandPlayback npc = npcs[i];
            if (npc == null)
            {
                continue;
            }

            float sqrDistance = (npc.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance <= nearestSqrDistance)
            {
                nearest = npc;
                nearestSqrDistance = sqrDistance;
            }
        }

        return nearest;
    }
}
