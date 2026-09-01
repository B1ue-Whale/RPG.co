using UnityEngine;

[RequireComponent(typeof(StatusEffectController))]
public class Racing_Gadget : GadgetBase
{
    //레이싱게임 : 자동차 - 이속 증가
    [SerializeField] private StatusEffectController statusEffectController;
    [SerializeField] private float speedBonus = 2f;
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

        Debug.Log("Racing Gadget Used!");
        statusEffectController.ApplyEffect(
            new StatusEffect(StatusEffectType.MoveSpeed, speedBonus, duration)
        );
        return true;
    }
}
