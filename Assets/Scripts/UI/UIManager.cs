using UnityEngine;

/// <summary>
/// Views�� UI ��� / ���� �� ���� ���
/// <para>���� ��ü ���� �̱���</para>
/// </summary>
public class UIManager : Singleton<UIManager>
{
    // UI 요소들 관리
    [SerializeField] private MainMenuController mainMenuController;
    [SerializeField] private LevelSelectionController levelSelectionController;
    [SerializeField] private PauseMenuController pauseMenuController;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        // 초기 UI 상태 설정
        if (GameManager.Instance != null)
        {
            UpdateUIForState(GameManager.Instance.CurrentState);
        }
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StateChanged += OnGameStateChanged;
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StateChanged -= OnGameStateChanged;
        }
    }

    private void OnGameStateChanged(Enums.GameState newState)
    {
        UpdateUIForState(newState);
    }

    private void UpdateUIForState(Enums.GameState state)
    {
        // 상태에 따른 UI 업데이트 로직
        switch (state)
        {
            case Enums.GameState.Main:
                Debug.Log("[UIManager] Main Menu State");
                break;

            case Enums.GameState.LevelSelection:
                Debug.Log("[UIManager] Level Selection State");
                break;

            case Enums.GameState.Level:
                Debug.Log("[UIManager] Level Playing State");
                break;

            case Enums.GameState.Paused:
                Debug.Log("[UIManager] Paused State");
                break;

            case Enums.GameState.WorldSelection:
                Debug.Log("[UIManager] World Selection State");
                break;

            case Enums.GameState.Story:
                Debug.Log("[UIManager] Story State");
                break;
        }
    }
}

