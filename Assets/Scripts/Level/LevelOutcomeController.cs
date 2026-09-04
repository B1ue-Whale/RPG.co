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
        _overlayRoot = OverlayMenuUi.CreateOverlayRoot("LevelResultOverlay", OverlayMenuUi.ResultSortingOrder);

        Image dim = OverlayMenuUi.CreateImage(_overlayRoot.transform, "Dim", OverlayMenuUi.DimColor);
        OverlayMenuUi.Stretch(dim.rectTransform);

        TMP_Text title = OverlayMenuUi.CreateText(
            _overlayRoot.transform,
            "Title",
            won ? "LEVEL CLEAR!" : "GAME OVER",
            OverlayMenuUi.TitleFontSize,
            won ? OverlayMenuUi.ClearColor : OverlayMenuUi.GameOverColor,
            _resolvedFont);
        OverlayMenuUi.Place(title.rectTransform, new Vector2(0f, 140f), OverlayMenuUi.TitleSize);

        TMP_Text reasonText = OverlayMenuUi.CreateText(
            _overlayRoot.transform,
            "Reason",
            reason,
            OverlayMenuUi.SubtitleFontSize,
            Color.white,
            _resolvedFont);
        OverlayMenuUi.Place(reasonText.rectTransform, new Vector2(0f, 40f), OverlayMenuUi.SubtitleSize);

        Button retry = OverlayMenuUi.CreateButton(
            _overlayRoot.transform, "RetryButton", "Retry", new Vector2(-170f, -100f), _resolvedFont);
        retry.onClick.AddListener(LevelTransition.RestartCurrentScene);

        Button levelSelect = OverlayMenuUi.CreateButton(
            _overlayRoot.transform, "LevelSelectButton", "Level Select", new Vector2(170f, -100f), _resolvedFont);
        levelSelect.onClick.AddListener(LevelTransition.GoToLevelSelection);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(retry.gameObject);
    }
}
