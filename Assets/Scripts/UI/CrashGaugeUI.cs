using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Temporary, minimal Crash Gauge display: reflects CrashGaugeManager state onto a
/// Slider and/or a text label. No behavior of its own beyond listening - all
/// gameplay logic stays in CrashGaugeManager/BugZone.
/// </summary>
public class CrashGaugeUI : MonoBehaviour
{
    [SerializeField] private CrashGaugeManager crashGaugeManager;
    [Tooltip("Optional. If assigned, its min/max are set to 0/MaxValue and its value is set directly from CurrentValue - same 0-100 scale as CrashGaugeManager, no normalization.")]
    [SerializeField] private Slider slider;
    [Tooltip("Optional. If assigned, shows 'current / max'.")]
    [SerializeField] private TMP_Text valueText;

    private void OnEnable()
    {
        if (crashGaugeManager == null)
            crashGaugeManager = FindFirstObjectByType<CrashGaugeManager>();

        if (crashGaugeManager == null)
        {
            Debug.LogWarning($"[{nameof(CrashGaugeUI)}] No {nameof(CrashGaugeManager)} found in the scene.");
            return;
        }

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = crashGaugeManager.MaxValue;
        }

        crashGaugeManager.GaugeChanged += OnGaugeChanged;
        crashGaugeManager.GaugeMaxed += OnGaugeMaxed;

        OnGaugeChanged(crashGaugeManager.CurrentValue);
    }

    private void OnDisable()
    {
        if (crashGaugeManager == null)
            return;

        crashGaugeManager.GaugeChanged -= OnGaugeChanged;
        crashGaugeManager.GaugeMaxed -= OnGaugeMaxed;
    }

    private void OnGaugeChanged(float value)
    {
        if (slider != null)
            slider.value = value;

        if (valueText != null)
            valueText.text = $"{Mathf.CeilToInt(value)} / {Mathf.CeilToInt(crashGaugeManager.MaxValue)}";
    }

    private void OnGaugeMaxed()
    {
        Debug.Log($"[{nameof(CrashGaugeUI)}] Crash Gauge maxed out.");
    }
}
