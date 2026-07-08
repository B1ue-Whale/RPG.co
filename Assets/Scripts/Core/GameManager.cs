using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// 게임의 전체 상태(로비, 인게임, 일시정지) 관리
/// 
/// 턴의 상태 관리(movestate, tag, attack....)
/// <para>게임 전체 전역 싱글톤</para>
/// </summary>
public class GameManager : Singleton<GameManager>
{
    public Enums.GameState CurrentState { get; private set; } = Enums.GameState.Main;

    public void ChangeState(Enums.GameState newState)
    {
        CurrentState = newState;

        // 상태 변경에 따른 로직

        Debug.Log($"Game State changed to: {CurrentState}");
    }

    // 추가적인 게임 관리 기능들
}
