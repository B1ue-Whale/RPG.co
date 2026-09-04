using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button testLevelButton;
    [SerializeField] private Button quitButton;

    [Header("Scene Names")]
    [SerializeField] private string levelSelectSceneName = "LevelSelectScene";
    [SerializeField] private string testSceneName = "World1_TestScene";

    [Header("World Select")]
    [SerializeField] private WorldSelectionController worldSelection;

    [Header("Look")]
    [Tooltip("Leave empty to use TextMesh Pro's default.")]
    [SerializeField] private TMP_FontAsset font;

    private GameObject buttonRoot;
    private TMP_FontAsset resolvedFont;

    private const float ButtonMargin = 72f;
    private const float ButtonSpacing = 24f;

    private void Start()
    {
        resolvedFont = font != null ? font : TMP_Settings.defaultFontAsset;

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

    public void OpenTestScene()
    {
        LevelTransition.EnterLevel(testSceneName);
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
        if (startButton != null)
            startButton.gameObject.SetActive(false);
        if (testLevelButton != null)
            testLevelButton.gameObject.SetActive(false);
        if (quitButton != null)
            quitButton.gameObject.SetActive(false);
    }

    private void BuildMenuButtons()
    {
        buttonRoot = OverlayMenuUi.CreateOverlayRoot(
            "MainMenuButtons",
            OverlayMenuUi.MainMenuSortingOrder);

        Vector2 size = OverlayMenuUi.MainMenuButtonSize;
        float y = ButtonMargin;
        CreateCornerButton("QuitButton", "Quit", y, size, QuitGame);

        y += size.y + ButtonSpacing;
        CreateCornerButton("TestButton", "Test", y, size, OpenTestScene);

        y += size.y + ButtonSpacing;
        CreateCornerButton("StartButton", "Start", y, size, OpenWorldSelection);
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
