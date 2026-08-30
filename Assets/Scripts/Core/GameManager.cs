using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// ������ ��ü ����(�κ�, �ΰ���, �Ͻ�����) ����
/// 
/// ���� ���� ����(movestate, tag, attack....)
/// <para>���� ��ü ���� �̱���</para>
/// </summary>
public class GameManager : Singleton<GameManager>
{
    public Enums.GameState CurrentState { get; private set; } = Enums.GameState.Main;

    public event Action<Enums.GameState> StateChanged;

    protected override void Awake()
    {
        base.Awake();
        CurrentState = Enums.GameState.Main;
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
}