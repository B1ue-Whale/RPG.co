using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LevelSelectionController : MonoBehaviour
{
    [System.Serializable]
    public class LevelEntry
    {
        public Button button;
        public string sceneName = string.Empty;
    }

    [Header("Buttons")]
    [SerializeField] private List<LevelEntry> levels = new List<LevelEntry>();
    [SerializeField] private Button backButton;

    private void Start()
    {
        RegisterLevelButtons();

        if (backButton != null)
            backButton.onClick.AddListener(GoBackToMainMenu);
    }

    private void RegisterLevelButtons()
    {
        foreach (var level in levels)
        {
            if (level.button == null)
            {
                Debug.LogWarning("[LevelSelectionController] A level button is not assigned.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(level.sceneName))
            {
                Debug.LogWarning("[LevelSelectionController] A level entry has an empty scene name.");
                continue;
            }

            level.button.onClick.AddListener(() => LevelTransition.EnterLevel(level.sceneName));
        }
    }

    public void GoBackToMainMenu()
    {
        LevelTransition.GoToMain();
    }
}
