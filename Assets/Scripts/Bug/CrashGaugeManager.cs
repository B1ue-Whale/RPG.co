using System;
using UnityEngine;

/// <summary>
/// Owns the Crash Gauge value (0-100) for the current level. Sources (e.g. BugZone)
/// call Add() every frame with a frame-rate-independent amount; this class only
/// clamps, tracks max-out state, and raises events for listeners (UI, lose condition).
/// Scene-scoped (not a Singleton/DontDestroyOnLoad) so a level restart/reload creates
/// a fresh instance with the gauge back at 0 for free.
/// </summary>
public class CrashGaugeManager : MonoBehaviour
{
    private const float MinValue = 0f;
    private const float MaxValueConst = 100f;

    public float CurrentValue { get; private set; }
    public float MaxValue => MaxValueConst;

    /// <summary>Raised whenever CurrentValue changes, with the new value.</summary>
    public event Action<float> GaugeChanged;

    /// <summary>Raised exactly once, the first time CurrentValue reaches MaxValue.</summary>
    public event Action GaugeMaxed;

    private bool hasMaxedOut;

    /// <summary>Adds to the gauge (amount should already be Time.deltaTime-scaled by the caller). No-op once maxed out.</summary>
    public void Add(float amount)
    {
        if (hasMaxedOut || amount <= 0f)
            return;

        float newValue = Mathf.Clamp(CurrentValue + amount, MinValue, MaxValueConst);
        if (newValue == CurrentValue)
            return;

        CurrentValue = newValue;
        GaugeChanged?.Invoke(CurrentValue);

        if (!hasMaxedOut && CurrentValue >= MaxValueConst)
        {
            hasMaxedOut = true;
            GaugeMaxed?.Invoke();
        }
    }

    /// <summary>Resets the gauge back to 0 and re-arms GaugeMaxed for a later trigger. Not needed for a scene reload (a new instance is created), but available for an in-place reset (e.g. NPC soft-death) if that's ever wanted.</summary>
    public void ResetGauge()
    {
        CurrentValue = MinValue;
        hasMaxedOut = false;
        GaugeChanged?.Invoke(CurrentValue);
    }
}
