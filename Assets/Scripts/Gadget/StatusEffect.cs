using UnityEngine;

public class StatusEffect 
{
    public StatusEffectType Type;
    public float Duration;
    public float Amount; 

    public StatusEffect(StatusEffectType type, float amount, float duration)
    {
        Type = type;
        Amount = amount;
        Duration = duration; 
    }
}
