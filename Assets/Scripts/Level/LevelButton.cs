using TMPro;
using UnityEngine;

public class LevelButton : MonoBehaviour
{
    [SerializeField] private string targetSceneName;
    [SerializeField] private string levelName;
    [SerializeField] private TextMeshProUGUI label;

    public void Initialize(string sceneName, string displayName)
    {
        targetSceneName = sceneName;
        levelName = displayName;

        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>();

        if (label != null)
            label.text = displayName;
    }

    public void EnterLevel()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("[LevelButton] targetSceneName is empty.");
            return;
        }

        LevelTransition.EnterLevel(targetSceneName);
    }
}
