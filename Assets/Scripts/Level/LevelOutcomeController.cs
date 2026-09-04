using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Decides the win/lose outcome of a level and shows the result screen.
/// <para>
/// Win:  the NPC reaches the end of its checkpoint chain (NpcProgressionController.
///       ChainCompleted - e.g. CP6 in World1_Level 1).
/// Lose: the NPC's suspicion meter fills up (NpcSuspicionController.SuspicionMaxed)
///       or the Crash Gauge maxes out (CrashGaugeManager.GaugeMaxed).
/// </para>
/// First outcome wins; everything after it is ignored. On any outcome the game is
/// frozen (Time.timeScale = 0), the state switches to Enums.GameState.Result (which
/// keeps the pause menu from opening on top), and an overlay with Retry / Level Select
/// buttons is shown. The overlay is built entirely from code, so this component works
/// with no scene wiring: drop it on any always-active object (e.g. LevelManager) and
/// the sources are found automatically if left unassigned.
/// </summary>
public class LevelOutcomeController : MonoBehaviour
{
    [Tooltip("Optional. Found automatically if left unassigned.")]
    [SerializeField] private NpcProgressionController npcProgression;
    [Tooltip("Optional. Found automatically if left unassigned.")]
    [SerializeField] private NpcSuspicionController npcSuspicion;
    [Tooltip("Optional. Found automatically if left unassigned.")]
    [SerializeField] private CrashGaugeManager crashGauge;

    [Header("Look")]
    [Tooltip("Font used by the result overlay. Leave empty to use TextMesh Pro's default.")]
    [SerializeField] private TMP_FontAsset font;

    private bool _outcomeDecided;
    private GameObject _overlayRoot;
    private TMP_FontAsset _resolvedFont;

    private void Start()
    {
        _resolvedFont = font != null ? font : TMP_Settings.defaultFontAsset;

        if (npcProgression == null)
            npcProgression = FindFirstObjectByType<NpcProgressionController>();
        if (npcSuspicion == null)
            npcSuspicion = FindFirstObjectByType<NpcSuspicionController>();
        if (crashGauge == null)
            crashGauge = FindFirstObjectByType<CrashGaugeManager>();

        if (npcProgression != null)
        {
            npcProgression.ChainCompleted += OnChainCompleted;

            // In case the chain already finished before this Start ran.
            if (npcProgression.IsChainComplete)
                OnChainCompleted();
        }
        else
        {
            Debug.LogWarning($"[{nameof(LevelOutcomeController)}] No {nameof(NpcProgressionController)} in the scene; the win condition can never trigger.");
        }

        if (npcSuspicion != null)
            npcSuspicion.SuspicionMaxed += OnSuspicionMaxed;
        else
            Debug.LogWarning($"[{nameof(LevelOutcomeController)}] No {nameof(NpcSuspicionController)} in the scene; the suspicion lose condition can never trigger.");

        if (crashGauge != null)
            crashGauge.GaugeMaxed += OnCrashGaugeMaxed;
        else
            Debug.LogWarning($"[{nameof(LevelOutcomeController)}] No {nameof(CrashGaugeManager)} in the scene; the crash-gauge lose condition can never trigger.");
    }

    private void OnDestroy()
    {
        if (npcProgression != null)
            npcProgression.ChainCompleted -= OnChainCompleted;
        if (npcSuspicion != null)
            npcSuspicion.SuspicionMaxed -= OnSuspicionMaxed;
        if (crashGauge != null)
            crashGauge.GaugeMaxed -= OnCrashGaugeMaxed;
    }

    private void OnChainCompleted()
    {
        ResolveOutcome(won: true, "The NPC reached the end of the level.");
    }

    private void OnSuspicionMaxed()
    {
        ResolveOutcome(won: false, "플레이어에게 걸렸습니다!");
    }

    private void OnCrashGaugeMaxed()
    {
        ResolveOutcome(won: false, "게임이 과한 버그로 크래시 났습니다..");
    }

    private void ResolveOutcome(bool won, string reason)
    {
        if (_outcomeDecided)
            return;

        _outcomeDecided = true;
        Debug.Log($"[{nameof(LevelOutcomeController)}] Level {(won ? "WON" : "LOST")}: {reason}");

        Time.timeScale = 0f;

        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(Enums.GameState.Result);

        ShowResultScreen(won, reason);
    }

    // ---------------------------------------------------------------------------
    // Result screen (built from code so no prefab/scene wiring is required).
    // ---------------------------------------------------------------------------

    private void ShowResultScreen(bool won, string reason)
    {
        _overlayRoot = new GameObject("LevelResultOverlay");

        var canvas = _overlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // Above pause menu / HUD.

        var scaler = _overlayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _overlayRoot.AddComponent<GraphicRaycaster>();

        // Full-screen dim.
        Image dim = CreateImage(_overlayRoot.transform, "Dim", new Color(0f, 0f, 0f, 0.75f));
        Stretch(dim.rectTransform);

        // Title.
        TMP_Text title = CreateText(_overlayRoot.transform, "Title",
            won ? "LEVEL CLEAR!" : "GAME OVER",
            96f,
            won ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.35f, 0.3f));
        Place(title.rectTransform, new Vector2(0f, 140f), new Vector2(1200f, 130f));

        // Reason line.
        TMP_Text reasonText = CreateText(_overlayRoot.transform, "Reason", reason, 36f, Color.white);
        Place(reasonText.rectTransform, new Vector2(0f, 40f), new Vector2(1200f, 60f));

        // Buttons.
        Button retry = CreateButton(_overlayRoot.transform, "RetryButton", "Retry", new Vector2(-170f, -100f));
        retry.onClick.AddListener(LevelTransition.RestartCurrentScene);

        Button levelSelect = CreateButton(_overlayRoot.transform, "LevelSelectButton", "Level Select", new Vector2(170f, -100f));
        levelSelect.onClick.AddListener(LevelTransition.GoToLevelSelection);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(retry.gameObject);
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private TMP_Text CreateText(Transform parent, string name, string text, float fontSize, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (_resolvedFont != null)
            tmp.font = _resolvedFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
    {
        Image background = CreateImage(parent, name, new Color(0.15f, 0.15f, 0.2f, 0.95f));
        Place(background.rectTransform, anchoredPosition, new Vector2(280f, 80f));

        var button = background.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.4f, 1f);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.15f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        TMP_Text text = CreateText(background.transform, "Label", label, 34f, Color.white);
        Stretch(text.rectTransform);

        return button;
    }

    /// <summary>Centered rect at the given anchored position/size.</summary>
    private static void Place(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    /// <summary>Stretches the rect to fill its parent.</summary>
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
