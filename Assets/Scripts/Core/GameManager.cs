using UnityEngine;
using UnityEngine.SceneManagement;
using System;

/// <summary>
/// 게임 전체 흐름(씬, 난이도, 일시정지) 관리
///
/// 게임 상태 관리(movestate, tag, attack....)
/// <para>게임 전체 흐름 싱글톤</para>
/// </summary>
[DefaultExecutionOrder(-1000)]
public class GameManager : Singleton<GameManager>
{
    public Enums.GameState CurrentState { get; private set; } = Enums.GameState.Main;

    public event Action<Enums.GameState> StateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ClearInstance();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        var go = new GameObject(nameof(GameManager));
        go.AddComponent<GameManager>();
    }

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        if (Instance != this)
            return;

        ApplyStateForScene(SceneManager.GetActiveScene());
    }

    protected override void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        base.OnDestroy();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        ApplyStateForScene(scene);
    }

    public void ChangeState(Enums.GameState newState)
    {
        if (CurrentState == newState)
            return;

        CurrentState = newState;
        StateChanged?.Invoke(newState);

        Debug.Log($"Game State changed to: {CurrentState}");
    }

    public bool IsState(Enums.GameState state)
    {
        return CurrentState == state;
    }

    private void ApplyStateForScene(Scene scene)
    {
        if (!scene.IsValid())
            return;

        ChangeState(InferStateFromScene(scene.name));
    }

    private static Enums.GameState InferStateFromScene(string sceneName)
    {
        switch (sceneName)
        {
            case "Main Scene":
                return Enums.GameState.Main;
            case "WorldSelectionScene":
                return Enums.GameState.WorldSelection;
            case "LevelSelectScene":
                return Enums.GameState.LevelSelection;
            default:
                return Enums.GameState.Level;
        }
    }
}
