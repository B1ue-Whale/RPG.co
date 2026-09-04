using UnityEngine;

/// <summary>
/// ������(Enums) ���� Ŭ����
/// </summary>
public static class Enums
{

    public enum GameState
    {
        Main,
        WorldSelection,
        LevelSelection,
        Level,
        Paused,
        Story,
        // A level has ended (win or lose) and the result screen is up. Blocks the
        // pause menu (PauseMenuController only toggles while in Level state).
        Result
    }

}