using UnityEngine;
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

    private void Start()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OpenLevelSelect);

        if (testLevelButton != null)
            testLevelButton.onClick.AddListener(OpenTestScene);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
    }

    public void OpenLevelSelect()
    {
        LevelTransition.GoToLevelSelection();
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
}
