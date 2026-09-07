using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button quitButton;

    [Header("Scene Names")]
    [SerializeField] private string levelSelectSceneName = "LevelSelectScene";

    [Header("World Select")]
    [SerializeField] private WorldSelectionController worldSelection;

    [Header("Look")]
    [Tooltip("Leave empty to use TextMesh Pro's default.")]
    [SerializeField] private TMP_FontAsset font;
    [Tooltip("Leave empty to use the same font as the menu buttons.")]
    [SerializeField] private TMP_FontAsset titleFont;

    private GameObject buttonRoot;
    private TMP_FontAsset resolvedFont;
    private TMP_FontAsset resolvedTitleFont;

    private const float ButtonMargin = 72f;
    private const float ButtonSpacing = 24f;

    private void Start()
    {
        resolvedFont = font != null ? font : TMP_Settings.defaultFontAsset;
        resolvedTitleFont = titleFont != null ? titleFont : resolvedFont;

        if (worldSelection == null)
            worldSelection = GetComponent<WorldSelectionController>();
        if (worldSelection == null)
            worldSelection = gameObject.AddComponent<WorldSelectionController>();

        worldSelection.Closed += OnWorldSelectionClosed;

        DisableLegacyButtons();
        BuildMenuButtons();
    }

    private void OnDestroy()
    {
        if (worldSelection != null)
            worldSelection.Closed -= OnWorldSelectionClosed;

        if (buttonRoot != null)
            Destroy(buttonRoot);
    }

    public void OpenWorldSelection()
    {
        SetMenuButtonsVisible(false);
        worldSelection.Open();
    }

    public void OpenLevelSelect()
    {
        LevelTransition.GoToLevelSelection(levelSelectSceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnWorldSelectionClosed()
    {
        SetMenuButtonsVisible(true);
    }

    private void SetMenuButtonsVisible(bool visible)
    {
        if (buttonRoot != null)
            buttonRoot.SetActive(visible);
    }

    private void DisableLegacyButtons()
    {
        Canvas legacyCanvas = null;
        if (startButton != null)
            legacyCanvas = startButton.GetComponentInParent<Canvas>();
        if (legacyCanvas == null && quitButton != null)
            legacyCanvas = quitButton.GetComponentInParent<Canvas>();

        if (legacyCanvas != null)
            legacyCanvas.gameObject.SetActive(false);

        HideLeftoverTestButtons();
    }

    private static void HideLeftoverTestButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            if (!IsTestButton(button))
                continue;

            button.gameObject.SetActive(false);
        }
    }

    private static bool IsTestButton(Button button)
    {
        if (button.name == "Test" || button.name == "TestButton")
            return true;

        TMP_Text tmp = button.GetComponentInChildren<TMP_Text>();
        if (tmp != null && tmp.text == "Test")
            return true;

        Text text = button.GetComponentInChildren<Text>();
        return text != null && text.text == "Test";
    }

    private void BuildMenuButtons()
    {
        buttonRoot = OverlayMenuUi.CreateOverlayRoot(
            "MainMenuButtons",
            OverlayMenuUi.MainMenuSortingOrder);

        CreateTitle();

        Vector2 size = OverlayMenuUi.MainMenuButtonSize;
        float y = ButtonMargin;
        CreateCornerButton("QuitButton", "Quit", y, size, QuitGame);

        y += size.y + ButtonSpacing;
        CreateCornerButton("StartButton", "Start", y, size, OpenWorldSelection);
    }

    private void CreateTitle()
    {
        TMP_Text title = OverlayMenuUi.CreateText(
            buttonRoot.transform,
            "Title",
            "RPG.co",
            OverlayMenuUi.MainMenuTitleFontSize,
            Color.white,
            resolvedTitleFont);

        title.alignment = TextAlignmentOptions.TopLeft;
        title.outlineWidth = 0.25f;
        title.outlineColor = new Color(0f, 0f, 0f, 0.85f);

        OverlayMenuUi.PlaceAnchored(
            title.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(ButtonMargin, -ButtonMargin),
            OverlayMenuUi.MainMenuTitleSize);
    }

    private void CreateCornerButton(
        string name,
        string label,
        float bottomOffset,
        Vector2 size,
        UnityAction action)
    {
        Button button = OverlayMenuUi.CreateButton(
            buttonRoot.transform,
            name,
            label,
            Vector2.zero,
            resolvedFont,
            size,
            OverlayMenuUi.MainMenuButtonLabelFontSize);

        OverlayMenuUi.PlaceAnchored(
            button.GetComponent<RectTransform>(),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-ButtonMargin, bottomOffset),
            size);

        button.onClick.AddListener(action);
    }
}
