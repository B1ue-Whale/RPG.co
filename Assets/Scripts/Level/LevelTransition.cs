using UnityEngine;
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

        Time.timeScale = 1f;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(Enums.GameState.Level);
        }

        SceneManager.LoadScene(sceneName);
    }

    public static void GoToMain()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(Enums.GameState.Main);

        Time.timeScale = 1f;
        SceneManager.LoadScene("Main Scene");
    }

    public static void GoToWorldSelection()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(Enums.GameState.WorldSelection);

        Time.timeScale = 1f;
        SceneManager.LoadScene("WorldSelectionScene");
    }

    public static void GoToLevelSelection()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(Enums.GameState.LevelSelection);

        Time.timeScale = 1f;
        SceneManager.LoadScene("LevelSelectScene");
    }

    public static void RestartCurrentScene()
    {
        Time.timeScale = 1f;

        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(Enums.GameState.Level);

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}