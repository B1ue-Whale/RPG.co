using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 게임 중 일시정지 메뉴를 열고 닫고, 입력/시간 흐름을 제어한다.
/// 오버레이는 승리/실패 스크린과 같은 스타일로 코드에서 생성한다.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private PlayerInput playerInput;

    [Header("Look")]
    [Tooltip("Title font. Leave empty to use TextMesh Pro's default.")]
    [SerializeField] private TMP_FontAsset font;
    [Tooltip("Button label font. Leave empty to use the title font.")]
    [SerializeField] private TMP_FontAsset buttonFont;

    private bool isPaused;
    private float previousTimeScale = 1f;
    private GameObject overlayRoot;
    private Button resumeButton;
    private TMP_FontAsset resolvedFont;
    private TMP_FontAsset resolvedButtonFont;

    private void Start()
    {
        resolvedFont = font != null ? font : TMP_Settings.defaultFontAsset;
        resolvedButtonFont = buttonFont != null ? buttonFont : resolvedFont;

        if (playerInput == null)
            playerInput = FindFirstObjectByType<PlayerInput>();

        BuildOverlay();
    }

    private void OnDestroy()
    {
        if (overlayRoot != null)
            Destroy(overlayRoot);
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    public void TogglePause()
    {
        if (GameManager.Instance != null
            && !GameManager.Instance.IsState(Enums.GameState.Level)
            && !GameManager.Instance.IsState(Enums.GameState.Paused))
            return;

        if (isPaused)
            Resume();
        else
            Pause();
    }

    public void Pause()
    {
        if (isPaused)
            return;

        isPaused = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(Enums.GameState.Paused);

        if (overlayRoot != null)
            overlayRoot.SetActive(true);

        SetPlayerInputEnabled(false);

        if (EventSystem.current != null && resumeButton != null)
            EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
    }

    public void Resume()
    {
        if (!isPaused)
            return;

        isPaused = false;
        Time.timeScale = previousTimeScale;

        if (overlayRoot != null)
            overlayRoot.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(Enums.GameState.Level);

        SetPlayerInputEnabled(true);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    public void RestartLevel()
    {
        Resume();
        LevelTransition.RestartCurrentScene();
    }

    public void OpenLevelSelection()
    {
        Resume();
        LevelTransition.GoToLevelSelection();
    }

    public void BackToMainMenu()
    {
        Resume();
        LevelTransition.GoToMain();
    }

    private void SetPlayerInputEnabled(bool enabled)
    {
        if (playerInput == null)
            return;

        if (enabled)
            playerInput.ActivateInput();
        else
            playerInput.DeactivateInput();
    }

    private void BuildOverlay()
    {
        overlayRoot = OverlayMenuUi.CreateOverlayRoot("PauseOverlay", OverlayMenuUi.PauseSortingOrder);

        Image dim = OverlayMenuUi.CreateImage(overlayRoot.transform, "Dim", OverlayMenuUi.DimColor);
        OverlayMenuUi.Stretch(dim.rectTransform);

        TMP_Text title = OverlayMenuUi.CreateText(
            overlayRoot.transform,
            "Title",
            "일시정지",
            OverlayMenuUi.TitleFontSize,
            OverlayMenuUi.PausedColor,
            resolvedFont);
        OverlayMenuUi.Place(title.rectTransform, new Vector2(0f, 180f), OverlayMenuUi.TitleSize);

        TMP_Text subtitle = OverlayMenuUi.CreateText(
            overlayRoot.transform,
            "Subtitle",
            "paused",
            OverlayMenuUi.SubtitleFontSize,
            Color.white,
            resolvedFont);
        OverlayMenuUi.Place(subtitle.rectTransform, new Vector2(0f, 90f), OverlayMenuUi.SubtitleSize);

        resumeButton = OverlayMenuUi.CreateButton(
            overlayRoot.transform, "ResumeButton", "재개", new Vector2(-170f, -20f), resolvedButtonFont);
        resumeButton.onClick.AddListener(Resume);

        Button restart = OverlayMenuUi.CreateButton(
            overlayRoot.transform, "RestartButton", "재시작", new Vector2(170f, -20f), resolvedButtonFont);
        restart.onClick.AddListener(RestartLevel);

        Button levelSelect = OverlayMenuUi.CreateButton(
            overlayRoot.transform, "LevelSelectButton", "레벨 선택", new Vector2(-170f, -120f), resolvedButtonFont);
        levelSelect.onClick.AddListener(OpenLevelSelection);

        Button mainMenu = OverlayMenuUi.CreateButton(
            overlayRoot.transform, "MainMenuButton", "메인 메뉴", new Vector2(170f, -120f), resolvedButtonFont);
        mainMenu.onClick.AddListener(BackToMainMenu);

        overlayRoot.SetActive(false);
    }
}
