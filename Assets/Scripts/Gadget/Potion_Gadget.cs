using UnityEngine;

[RequireComponent(typeof(StatusEffectController))]
public class Potion_Gadget : GadgetBase
{
    //마나 포션: 버그 삭제 시간 줄이기
    [SerializeField] private StatusEffectController statusEffectController;
    [SerializeField] private float cleanseTimeReduction = 1.5f;
    [SerializeField] private float duration = 5f;


    private void Awake()
    {
        if (statusEffectController == null)
        {
            statusEffectController = GetComponent<StatusEffectController>();
        }
    }

    protected override bool Use()
    {
        if (statusEffectController == null)
        {
            Debug.LogWarning("StatusEffectController가 연결되지 않았습니다.");
            return false;
        }

        Debug.Log("Potion Used!");
        statusEffectController.ApplyEffect(
          new StatusEffect(StatusEffectType.BugCooldown, cleanseTimeReduction, duration)
      );
        return true;
    }
}
