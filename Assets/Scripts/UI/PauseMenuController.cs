using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 게임 중 일시정지 메뉴를 열고 닫고, 입력/시간 흐름을 제어한다.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    [Header("Pause UI")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Selectable firstSelected;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button levelSelectButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Input")]
    [SerializeField] private PlayerInput playerInput;

    private bool isPaused;
    private float previousTimeScale = 1f;

    private void Start()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(Resume);

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartLevel);

        if (levelSelectButton != null)
            levelSelectButton.onClick.AddListener(OpenLevelSelection);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(BackToMainMenu);
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsState(Enums.GameState.Level))
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

        if (pausePanel != null)
            pausePanel.SetActive(true);

        if (playerInput != null)
        {
            if (playerInput.actions != null && playerInput.actions.FindActionMap("UI") != null)
                playerInput.SwitchCurrentActionMap("UI");
        }

        if (EventSystem.current != null && firstSelected != null)
            EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
    }

    public void Resume()
    {
        if (!isPaused)
            return;

        isPaused = false;
        Time.timeScale = previousTimeScale;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(Enums.GameState.Level);

        if (playerInput != null)
        {
            if (playerInput.actions != null && playerInput.actions.FindActionMap("Player") != null)
                playerInput.SwitchCurrentActionMap("Player");
        }
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
}
