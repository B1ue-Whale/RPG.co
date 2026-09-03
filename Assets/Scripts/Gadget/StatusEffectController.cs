using System.Collections.Generic;
using UnityEngine;

public class StatusEffectController : MonoBehaviour
{
    private readonly List<StatusEffect> activeEffects
        = new List<StatusEffect>();

    public void ApplyEffect(StatusEffect effect)
    {
        activeEffects.Add(effect);
    }

    private void Update()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            activeEffects[i].Duration -= Time.deltaTime;

            if (activeEffects[i].Duration <= 0f)
            {
                activeEffects.RemoveAt(i);
            }
        }
    }

    public bool HasEffect(StatusEffectType type)
    {
        foreach (StatusEffect effect in activeEffects)
        {
            if (effect.Type == type)
                return true;
        }

        return false;
    }

    public float GetValue(StatusEffectType type)
    {
        float total = 0f;

        foreach (StatusEffect effect in activeEffects)
        {
            if (effect.Type == type)
            {
                total += effect.Amount;
            }
        }

        return total;
    }
}