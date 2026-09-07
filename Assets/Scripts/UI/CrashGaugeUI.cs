using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays CrashGaugeManager as a probability percentage (e.g. "Crash probability: 68%").
/// Hides any leftover Slider visuals so existing scenes keep working without rewiring.
/// </summary>
public class CrashGaugeUI : MonoBehaviour
{
    private const string DefaultFormat = "Crash probability: {0}%";
    private const float RapidColorHold = 0.15f;
    private static readonly Vector2 LabelAnchor = new Vector2(0f, 1f);
    private static readonly Vector2 LabelPivot = new Vector2(0f, 1f);
    private static readonly Vector2 LabelPosition = new Vector2(36f, -20f);
    private static readonly Vector2 LabelSize = new Vector2(920f, 72f);

    [SerializeField] private CrashGaugeManager crashGaugeManager;
    [Tooltip("Legacy fill bar. Hidden at runtime if assigned.")]
    [SerializeField] private Slider slider;
    [Tooltip("Optional. Created at runtime if left unassigned.")]
    [SerializeField] private TMP_Text valueText;
    [Tooltip("Label format. {0} is the current percentage.")]
    [SerializeField] private string labelFormat = DefaultFormat;

    [Header("Look")]
    [Tooltip("TMP font for the probability label. Leave empty to use the scene HUD font, then TextMesh Pro's default.")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float fontSize = 42f;
    [SerializeField] private Color calmColor = new Color(0.65f, 1f, 0.55f);
    [SerializeField] private Color rapidRiseColor = new Color(1f, 0.22f, 0.22f);
    [Tooltip("Points per second that count as a rapid rise. One NPC-nearby infected tile is 10 by default; aged tiles are 2 each.")]
    [SerializeField] private float rapidRiseThreshold = 8f;

    private TMP_Text _runtimeText;
    private float _lastValue;
    private float _rapidUntil;

    private void OnEnable()
    {
        if (crashGaugeManager == null)
            crashGaugeManager = FindFirstObjectByType<CrashGaugeManager>();

        if (crashGaugeManager == null)
        {
            Debug.LogWarning($"[{nameof(CrashGaugeUI)}] No {nameof(CrashGaugeManager)} found in the scene.");
            return;
        }

        OverlayMenuUi.ConfigureScaler(GetComponentInParent<Canvas>());
        HideSliderVisuals();
        EnsureValueText();
        ApplyLook();

        _lastValue = crashGaugeManager.CurrentValue;
        _rapidUntil = 0f;

        crashGaugeManager.GaugeChanged += OnGaugeChanged;
        crashGaugeManager.GaugeMaxed += OnGaugeMaxed;

        RefreshLabel(crashGaugeManager.CurrentValue);
        ApplyColor(false);
    }

    private void OnDisable()
    {
        if (crashGaugeManager == null)
            return;

        crashGaugeManager.GaugeChanged -= OnGaugeChanged;
        crashGaugeManager.GaugeMaxed -= OnGaugeMaxed;
    }

    private void Update()
    {
        if (crashGaugeManager == null || valueText == null)
            return;

        float current = crashGaugeManager.CurrentValue;
        if (Time.deltaTime > 0f)
        {
            float rate = (current - _lastValue) / Time.deltaTime;
            _lastValue = current;

            if (rate >= rapidRiseThreshold)
                _rapidUntil = Time.unscaledTime + RapidColorHold;
        }

        ApplyColor(Time.unscaledTime < _rapidUntil);
    }

    private void HideSliderVisuals()
    {
        if (slider == null)
            slider = GetComponent<Slider>();

        if (slider == null)
            return;

        slider.enabled = false;

        for (int i = 0; i < slider.transform.childCount; i++)
        {
            Transform child = slider.transform.GetChild(i);
            if (valueText != null && child == valueText.transform)
                continue;
            child.gameObject.SetActive(false);
        }
    }

    private void EnsureValueText()
    {
        if (valueText == null && _runtimeText != null)
            valueText = _runtimeText;

        if (valueText == null)
        {
            Transform parent = transform.parent != null ? transform.parent : transform;
            TMP_FontAsset resolvedFont = font != null ? font : FindHudFont(parent);
            if (resolvedFont == null)
                resolvedFont = TMP_Settings.defaultFontAsset;

            valueText = OverlayMenuUi.CreateText(
                parent,
                "CrashProbability",
                FormatLabel(0f),
                fontSize,
                calmColor,
                resolvedFont);
            valueText.outlineWidth = 0.2f;
            valueText.outlineColor = Color.black;
            valueText.enableWordWrapping = false;
            _runtimeText = valueText;
        }

        LayoutValueText();
    }

    private void LayoutValueText()
    {
        if (valueText == null)
            return;

        valueText.alignment = TextAlignmentOptions.TopLeft;
        OverlayMenuUi.PlaceAnchored(
            valueText.rectTransform,
            LabelAnchor,
            LabelPivot,
            LabelPosition,
            LabelSize);
    }

    private void ApplyLook()
    {
        if (valueText == null)
            return;

        if (font != null)
            valueText.font = font;

        valueText.fontSize = fontSize;
    }

    private void ApplyColor(bool rapid)
    {
        if (valueText != null)
            valueText.color = rapid ? rapidRiseColor : calmColor;
    }

    private static TMP_FontAsset FindHudFont(Transform parent)
    {
        TMP_Text[] labels = parent.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null && labels[i].font != null)
                return labels[i].font;
        }

        return null;
    }

    private void OnGaugeChanged(float value)
    {
        RefreshLabel(value);
    }

    private void RefreshLabel(float value)
    {
        if (valueText != null)
            valueText.text = FormatLabel(value);
    }

    private string FormatLabel(float value)
    {
        float max = crashGaugeManager != null ? crashGaugeManager.MaxValue : 100f;
        int percent = Mathf.Clamp(Mathf.RoundToInt(max > 0f ? value / max * 100f : 0f), 0, 100);
        string format = string.IsNullOrEmpty(labelFormat) ? DefaultFormat : labelFormat;
        return string.Format(format, percent);
    }

    private void OnGaugeMaxed()
    {
        Debug.Log($"[{nameof(CrashGaugeUI)}] Crash Gauge maxed out.");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyLook();
    }
#endif
}
