using UnityEngine.SceneManagement;

public static class LevelTransition
{
    public static void EnterLevel(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

 
}