using UnityEngine;

public class LevelButton : MonoBehaviour
{
    [SerializeField]
    private string targetSceneName;

    public void EnterLevel()
    {
        LevelTransition.EnterLevel(targetSceneName);
    }
}
