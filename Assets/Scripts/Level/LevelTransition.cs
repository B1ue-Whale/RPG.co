using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class LevelTransition
{
    public static void EnterLevel(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[LevelTransition] sceneName is empty.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[LevelTransition] Scene not found in Build Settings: {sceneName}");
            return;
        }

        RestoreGameplayInput();

        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(Enums.GameState.Level);

        SceneManager.LoadScene(sceneName);
    }

    public static void GoToMain()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(Enums.GameState.Main);

        RestoreGameplayInput();
        SceneManager.LoadScene("Main Scene");
    }

    public static void GoToWorldSelection()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(Enums.GameState.WorldSelection);

        RestoreGameplayInput();
        SceneManager.LoadScene("WorldSelectionScene");
    }

    public static void GoToLevelSelection()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(Enums.GameState.LevelSelection);

        RestoreGameplayInput();
        SceneManager.LoadScene("LevelSelectScene");
    }

    public static void RestartCurrentScene()
    {
        RestoreGameplayInput();

        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(Enums.GameState.Level);

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Re-enables gameplay input after pause/result (timeScale 0, UI action map).
    /// PlayerInput shares the project InputActionAsset, so a map switch can stick
    /// across scene loads if Default Action Map is empty.
    /// </summary>
    public static void RestoreGameplayInput()
    {
        Time.timeScale = 1f;

        EnablePlayerMap(InputSystem.actions);

        PlayerInput[] playerInputs = Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        for (int i = 0; i < playerInputs.Length; i++)
        {
            PlayerInput playerInput = playerInputs[i];
            EnablePlayerMap(playerInput.actions);

            if (playerInput.actions != null && playerInput.actions.FindActionMap("Player") != null)
                playerInput.SwitchCurrentActionMap("Player");

            playerInput.ActivateInput();
        }

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private static void EnablePlayerMap(InputActionAsset asset)
    {
        if (asset == null)
            return;

        InputActionMap playerMap = asset.FindActionMap("Player");
        if (playerMap != null && !playerMap.enabled)
            playerMap.Enable();
    }
}