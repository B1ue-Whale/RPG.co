using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays CrashGaugeManager as a compact semicircular danger meter (needle + a few
/// tick marks + percentage text), instead of a bar. Hides any leftover Slider visuals
/// so existing scenes keep working without rewiring.
/// </summary>
[ExecuteAlways]
public class CrashGaugeUI : MonoBehaviour
{
    private const string DefaultFormat = "{0}%";
    private const float RapidColorHold = 0.15f;

    [SerializeField] private CrashGaugeManager crashGaugeManager;
    [Tooltip("Legacy fill bar. Hidden at runtime if assigned.")]
    [SerializeField] private Slider slider;
    [Tooltip("Percentage text. Created at runtime if left unassigned. If assigned to a text that isn't part of an auto-built visual root, the gauge is skipped and this behaves like a plain label (legacy behavior).")]
    [SerializeField] private TMP_Text valueText;
    [Tooltip("Label format for the percentage readout. {0} is the current percentage.")]
    [SerializeField] private string labelFormat = DefaultFormat;

    [Header("Look")]
    [Tooltip("TMP font. Leave empty to use the scene HUD font, then TextMesh Pro's default.")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float fontSize = 68f;
    [SerializeField] private Color calmColor = new Color(0.65f, 1f, 0.55f);
    [SerializeField] private Color rapidRiseColor = new Color(1f, 0.22f, 0.22f);
    [Tooltip("Points per second that count as a rapid rise. One NPC-nearby infected tile is 10 by default; aged tiles are 2 each.")]
    [SerializeField] private float rapidRiseThreshold = 8f;
    [Tooltip("Seconds for the needle/ticks/percentage text to ease toward a sudden jump in the gauge (e.g. an NPC death reducing it in one step), instead of snapping instantly. 0 = instant, matches the raw value exactly.")]
    [SerializeField] private float displaySmoothTime = 0.35f;
    [Tooltip("A dim backing panel behind the whole gauge so it reads against busy backgrounds. Alpha 0 = no background.")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] private float backgroundPadding = 10f;

    [Header("Gauge")]
    [SerializeField] private float arcRadius = 72f;
    [SerializeField] private float needleLength = 60f;
    [SerializeField] private float needleThickness = 8f;
    [SerializeField] private Color needlePivotColor = new Color(0.85f, 0.85f, 0.9f);
    [SerializeField] private float pivotDotSize = 16f;
    [Tooltip("Needle angle in degrees at 0% crash. 90 points straight left.")]
    [SerializeField] private float needleMinAngle = 90f;
    [Tooltip("Needle angle in degrees at 100% crash. -90 points straight right.")]
    [SerializeField] private float needleMaxAngle = -90f;

    [Header("Ticks")]
    [Tooltip("Simple dash marks along the sweep. Kept small - not a fully labeled dial.")]
    [SerializeField] private int tickCount = 5;
    [SerializeField] private float tickLength = 14f;
    [SerializeField] private float tickThickness = 6f;
    [SerializeField] private Color tickColor = new Color(0.55f, 0.55f, 0.62f, 0.9f);
    [Tooltip("Ticks at or beyond this percent along the sweep are tinted as the danger zone (like a redline).")]
    [SerializeField] private float dangerZoneStartPercent = 70f;
    [SerializeField] private Color dangerZoneTickColor = new Color(1f, 0.45f, 0.3f, 0.95f);

    [Header("Danger Feedback")]
    [Tooltip("Percent at which the danger-zone ticks start to softly pulse.")]
    [SerializeField] private float pulseStartPercent = 70f;
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.15f;
    [Tooltip("Percent at which the needle starts a very small jitter.")]
    [SerializeField] private float jitterStartPercent = 85f;
    [SerializeField] private float jitterMinAngle = 0.4f;
    [SerializeField] private float jitterMaxAngle = 4f;
    [SerializeField] private float jitterSpeed = 14f;
    [Tooltip("Percent at which the percentage text starts a subtle pulse.")]
    [SerializeField] private float textPulseStartPercent = 92f;
    [SerializeField] private float textPulseSpeed = 6f;

    [SerializeField, HideInInspector] private RectTransform visualRoot;
    [SerializeField, HideInInspector] private Image background;
    [SerializeField, HideInInspector] private RectTransform arcContainer;
    [SerializeField, HideInInspector] private RectTransform pivot;
    [SerializeField, HideInInspector] private RectTransform needleRotator;
    [SerializeField, HideInInspector] private Image needleImage;
    [SerializeField, HideInInspector] private Image pivotDot;
    [SerializeField, HideInInspector] private Image[] ticks;

    private float _lastValue;
    private float _rapidUntil;
    private float _noiseSeed;
    private float _displayValue;
    private float _displayVelocity;

    private void OnEnable()
    {
        if (crashGaugeManager == null)
            crashGaugeManager = FindFirstObjectByType<CrashGaugeManager>();

        if (crashGaugeManager == null)
        {
            Debug.LogWarning($"[{nameof(CrashGaugeUI)}] No {nameof(CrashGaugeManager)} found in the scene.");
            return;
        }

        // Canvas proportion fix from main: keep the HUD canvas scaler consistent.
        // Play-mode only so [ExecuteAlways] doesn't rewrite scaler settings in the editor.
        if (Application.isPlaying)
            OverlayMenuUi.ConfigureScaler(GetComponentInParent<Canvas>());
        HideSliderVisuals();
        EnsureVisuals();
        ApplyLook();

        _lastValue = crashGaugeManager.CurrentValue;
        _rapidUntil = 0f;
        _noiseSeed = (GetEntityId().GetHashCode() % 1000) * 0.137f;
        _displayValue = crashGaugeManager.CurrentValue;
        _displayVelocity = 0f;

        crashGaugeManager.GaugeMaxed += OnGaugeMaxed;

        if (valueText != null)
            valueText.text = FormatLabel(_displayValue);
    }

    private void OnDisable()
    {
        if (crashGaugeManager == null)
            return;

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

        bool rapid = Time.unscaledTime < _rapidUntil;

        // The needle/ticks/text ease toward drastic jumps (e.g. an NPC death reducing
        // the gauge in one step) instead of snapping - the rapid-rise color flash above
        // already gives instant feedback on the raw value, so easing the motion here
        // doesn't hide anything.
        _displayValue = displaySmoothTime > 0f && Application.isPlaying
            ? Mathf.SmoothDamp(_displayValue, current, ref _displayVelocity, displaySmoothTime)
            : current;

        float max = crashGaugeManager.MaxValue;
        float percent = max > 0f ? Mathf.Clamp(_displayValue / max * 100f, 0f, 100f) : 0f;

        float severity = Mathf.InverseLerp(dangerZoneStartPercent, 100f, percent);
        Color baseColor = Color.Lerp(calmColor, rapidRiseColor, Mathf.Clamp01(severity));
        Color activeColor = rapid ? rapidRiseColor : baseColor;

        valueText.text = FormatLabel(_displayValue);
        UpdateNeedle(percent, activeColor);
        UpdateTickEmphasis(percent);
        UpdateTextFeedback(percent, activeColor);
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

    /// <summary>
    /// Builds (or reuses, across domain reloads - see the HideInInspector fields) the
    /// arc/needle/ticks/percentage readout. Detects and discards visuals left over from
    /// an earlier version of this component (a bar or segmented layout) rather than
    /// silently treating them as valid, since that has bitten this component before.
    /// </summary>
    private void EnsureVisuals()
    {
        if (visualRoot != null)
        {
            if (arcContainer == null || needleRotator == null || needleImage == null || pivot == null || background == null)
            {
                DestroyVisualRoot();
            }
            else
            {
                RebuildTicksIfNeeded();
                return;
            }
        }

        if (valueText != null)
        {
            bool staleAutoCreated = valueText.name == "CrashProbability" || valueText.name == "Percent";
            if (staleAutoCreated)
            {
                GameObject stale = valueText.gameObject;
                valueText = null;
                if (Application.isPlaying)
                    Destroy(stale);
                else
                    DestroyImmediate(stale);
            }
            else
            {
                // Genuine manual wiring predating the gauge redesign - just keep updating it.
                return;
            }
        }

        BuildVisuals();
    }

    private void DestroyVisualRoot()
    {
        if (visualRoot != null)
        {
            GameObject go = visualRoot.gameObject;
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        visualRoot = null;
        background = null;
        arcContainer = null;
        pivot = null;
        needleRotator = null;
        needleImage = null;
        pivotDot = null;
        ticks = null;
        valueText = null;
    }

    private void BuildVisuals()
    {
        Transform parent = transform.parent != null ? transform.parent : transform;
        TMP_FontAsset resolvedFont = font != null ? font : FindHudFont(parent);
        if (resolvedFont == null)
            resolvedFont = TMP_Settings.defaultFontAsset;

        var rootGo = new GameObject("CrashGaugeVisual", typeof(RectTransform));
        rootGo.transform.SetParent(parent, false);
        visualRoot = rootGo.GetComponent<RectTransform>();

        var columns = rootGo.AddComponent<VerticalLayoutGroup>();
        columns.spacing = 4f;
        columns.childAlignment = TextAnchor.UpperCenter;
        columns.childControlWidth = true;
        columns.childControlHeight = true;
        columns.childForceExpandWidth = false;
        columns.childForceExpandHeight = false;

        var rootFitter = rootGo.AddComponent<ContentSizeFitter>();
        rootFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        background = OverlayMenuUi.CreateImage(visualRoot, "Background", backgroundColor);
        background.raycastTarget = false;
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = new Vector2(-backgroundPadding, -backgroundPadding);
        backgroundRect.offsetMax = new Vector2(backgroundPadding, backgroundPadding);
        var backgroundLayout = background.gameObject.AddComponent<LayoutElement>();
        backgroundLayout.ignoreLayout = true;

        var arcGo = new GameObject("Arc", typeof(RectTransform));
        arcGo.transform.SetParent(visualRoot, false);
        arcContainer = arcGo.GetComponent<RectTransform>();
        var arcLayout = arcGo.AddComponent<LayoutElement>();
        arcLayout.minWidth = arcLayout.preferredWidth = arcRadius * 2f;
        arcLayout.minHeight = arcLayout.preferredHeight = arcRadius + pivotDotSize;

        var pivotGo = new GameObject("Pivot", typeof(RectTransform));
        pivotGo.transform.SetParent(arcContainer, false);
        pivot = pivotGo.GetComponent<RectTransform>();
        pivot.anchorMin = pivot.anchorMax = new Vector2(0.5f, 0f);
        pivot.pivot = new Vector2(0.5f, 0f);
        pivot.anchoredPosition = Vector2.zero;
        pivot.sizeDelta = Vector2.zero;

        BuildTicks();

        var needleRotatorGo = new GameObject("NeedleRotator", typeof(RectTransform));
        needleRotatorGo.transform.SetParent(pivot, false);
        needleRotator = needleRotatorGo.GetComponent<RectTransform>();
        needleRotator.anchorMin = needleRotator.anchorMax = new Vector2(0.5f, 0f);
        needleRotator.pivot = new Vector2(0.5f, 0f);
        needleRotator.anchoredPosition = Vector2.zero;
        needleRotator.sizeDelta = Vector2.zero;

        needleImage = OverlayMenuUi.CreateImage(needleRotator, "Needle", calmColor);
        RectTransform needleRect = needleImage.rectTransform;
        needleRect.anchorMin = needleRect.anchorMax = new Vector2(0.5f, 0f);
        needleRect.pivot = new Vector2(0.5f, 0f);
        needleRect.anchoredPosition = Vector2.zero;
        needleRect.sizeDelta = new Vector2(needleThickness, needleLength);

        pivotDot = OverlayMenuUi.CreateImage(pivot, "PivotDot", needlePivotColor);
        RectTransform dotRect = pivotDot.rectTransform;
        dotRect.anchorMin = dotRect.anchorMax = new Vector2(0.5f, 0f);
        dotRect.pivot = new Vector2(0.5f, 0.5f);
        dotRect.anchoredPosition = Vector2.zero;
        dotRect.sizeDelta = new Vector2(pivotDotSize, pivotDotSize);

        valueText = OverlayMenuUi.CreateText(visualRoot, "Percent", FormatLabel(0f), fontSize, calmColor, resolvedFont);
        valueText.outlineWidth = 0.2f;
        valueText.outlineColor = Color.black;
        valueText.enableWordWrapping = false;
        valueText.alignment = TextAlignmentOptions.Center;
        var percentLayout = valueText.gameObject.AddComponent<LayoutElement>();
        percentLayout.minWidth = fontSize * 2.6f;

        if (transform is RectTransform source)
        {
            visualRoot.anchorMin = visualRoot.anchorMax = source.anchorMin;
            visualRoot.pivot = source.pivot;
            visualRoot.anchoredPosition = source.anchoredPosition;
            columns.childAlignment = CornerAlignment(source.pivot);
        }
    }

    private void RebuildTicksIfNeeded()
    {
        if (pivot == null)
            return;

        if (ticks != null && ticks.Length == Mathf.Max(2, tickCount))
        {
            SyncTicks();
            return;
        }

        BuildTicks();
    }

    private void BuildTicks()
    {
        if (pivot == null)
            return;

        if (ticks != null)
        {
            for (int i = 0; i < ticks.Length; i++)
            {
                if (ticks[i] == null)
                    continue;

                GameObject rotatorGo = ticks[i].transform.parent != null ? ticks[i].transform.parent.gameObject : ticks[i].gameObject;
                if (Application.isPlaying)
                    Destroy(rotatorGo);
                else
                    DestroyImmediate(rotatorGo);
            }
        }

        int count = Mathf.Max(2, tickCount);
        ticks = new Image[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float angle = Mathf.Lerp(needleMinAngle, needleMaxAngle, t);

            var rotatorGo = new GameObject($"TickRotator_{i}", typeof(RectTransform));
            rotatorGo.transform.SetParent(pivot, false);
            var rotatorRect = (RectTransform)rotatorGo.transform;
            rotatorRect.anchorMin = rotatorRect.anchorMax = new Vector2(0.5f, 0f);
            rotatorRect.pivot = new Vector2(0.5f, 0f);
            rotatorRect.anchoredPosition = Vector2.zero;
            rotatorRect.sizeDelta = Vector2.zero;
            rotatorRect.localEulerAngles = new Vector3(0f, 0f, angle);

            bool danger = t * 100f >= dangerZoneStartPercent;
            Image tick = OverlayMenuUi.CreateImage(rotatorRect, $"Tick_{i}", danger ? dangerZoneTickColor : tickColor);
            RectTransform tickRect = tick.rectTransform;
            tickRect.anchorMin = tickRect.anchorMax = new Vector2(0.5f, 0f);
            tickRect.pivot = new Vector2(0.5f, 0f);
            tickRect.anchoredPosition = new Vector2(0f, arcRadius - tickLength);
            tickRect.sizeDelta = new Vector2(tickThickness, tickLength);

            ticks[i] = tick;
        }
    }

    private void SyncTicks()
    {
        int count = ticks.Length;
        for (int i = 0; i < count; i++)
        {
            if (ticks[i] == null)
                continue;

            float t = (float)i / (count - 1);
            float angle = Mathf.Lerp(needleMinAngle, needleMaxAngle, t);

            if (ticks[i].transform.parent != null)
                ticks[i].transform.parent.localEulerAngles = new Vector3(0f, 0f, angle);

            RectTransform tickRect = ticks[i].rectTransform;
            tickRect.anchoredPosition = new Vector2(0f, arcRadius - tickLength);
            tickRect.sizeDelta = new Vector2(tickThickness, tickLength);

            bool danger = t * 100f >= dangerZoneStartPercent;
            ticks[i].color = danger ? dangerZoneTickColor : tickColor;
        }
    }

    private static TextAnchor CornerAlignment(Vector2 pivot)
    {
        bool left = pivot.x <= 0.25f;
        bool right = pivot.x >= 0.75f;

        if (left) return TextAnchor.UpperLeft;
        if (right) return TextAnchor.UpperRight;
        return TextAnchor.UpperCenter;
    }

    private void ApplyLook()
    {
        if (valueText != null)
        {
            if (font != null)
                valueText.font = font;
            valueText.fontSize = fontSize;
        }

        if (arcContainer != null && arcContainer.TryGetComponent(out LayoutElement arcLayout))
        {
            arcLayout.minWidth = arcLayout.preferredWidth = arcRadius * 2f;
            arcLayout.minHeight = arcLayout.preferredHeight = arcRadius + pivotDotSize;
        }

        if (background != null)
        {
            background.color = backgroundColor;
            background.rectTransform.offsetMin = new Vector2(-backgroundPadding, -backgroundPadding);
            background.rectTransform.offsetMax = new Vector2(backgroundPadding, backgroundPadding);
        }

        if (needleImage != null)
            needleImage.rectTransform.sizeDelta = new Vector2(needleThickness, needleLength);

        if (pivotDot != null)
        {
            pivotDot.color = needlePivotColor;
            pivotDot.rectTransform.sizeDelta = new Vector2(pivotDotSize, pivotDotSize);
        }

        RebuildTicksIfNeeded();
    }

    private void UpdateNeedle(float percent, Color color)
    {
        float t = percent / 100f;
        float angle = Mathf.Lerp(needleMinAngle, needleMaxAngle, t);

        if (Application.isPlaying)
            angle += ComputeNeedleJitter(percent);

        if (needleRotator != null)
            needleRotator.localRotation = Quaternion.Euler(0f, 0f, angle);

        if (needleImage != null)
            needleImage.color = color;
    }

    private float ComputeNeedleJitter(float percent)
    {
        if (percent < jitterStartPercent)
            return 0f;

        float severity = Mathf.InverseLerp(jitterStartPercent, 100f, percent);
        float amplitude = Mathf.Lerp(jitterMinAngle, jitterMaxAngle, severity);
        float noise = Mathf.PerlinNoise(Time.unscaledTime * jitterSpeed + _noiseSeed, _noiseSeed) * 2f - 1f;
        return noise * amplitude;
    }

    private void UpdateTickEmphasis(float percent)
    {
        if (ticks == null)
            return;

        bool warm = Application.isPlaying && percent >= pulseStartPercent;
        float wave = warm ? 0.5f * (1f + Mathf.Sin(Time.unscaledTime * pulseSpeed)) : 0f;
        int count = ticks.Length;

        for (int i = 0; i < count; i++)
        {
            if (ticks[i] == null)
                continue;

            float t = (float)i / (count - 1);
            bool danger = t * 100f >= dangerZoneStartPercent;
            if (!danger)
            {
                ticks[i].color = tickColor;
                continue;
            }

            ticks[i].color = warm ? Color.Lerp(dangerZoneTickColor, Color.white, wave * 0.5f) : dangerZoneTickColor;
        }
    }

    private void UpdateTextFeedback(float percent, Color color)
    {
        valueText.color = color;

        if (!Application.isPlaying || percent < textPulseStartPercent)
        {
            valueText.transform.localScale = Vector3.one;
            return;
        }

        float severity = Mathf.InverseLerp(textPulseStartPercent, 100f, percent);
        float amplitude = pulseAmount * Mathf.Clamp01(severity) * 0.5f;
        float wave = 0.5f * (1f + Mathf.Sin(Time.unscaledTime * textPulseSpeed));
        float scale = 1f + amplitude * wave;
        valueText.transform.localScale = new Vector3(scale, scale, 1f);
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
    private bool _applyLookScheduled;

    private void OnValidate()
    {
        if (visualRoot == null || _applyLookScheduled)
            return;

        // Rebuilding ticks (creating/destroying GameObjects) can't happen synchronously
        // inside OnValidate - Unity logs "SendMessage cannot be called during Awake,
        // CheckConsistency, or OnValidate" and the rebuild doesn't fully take effect
        // (e.g. lowering the tick count silently failing). Defer it a beat instead.
        _applyLookScheduled = true;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            _applyLookScheduled = false;
            if (this != null)
                ApplyLook();
        };
    }
#endif
}
