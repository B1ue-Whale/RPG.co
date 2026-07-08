using UnityEngine;

/// <summary>
/// 스테이지의 기본 데이터(이름, 인덱스, 게임 규칙, 배치 정보 등) 보관
/// <para>내장 맵 전용 ScriptableObject</para>
/// <para>내부적으로 MapData로 변환하여 사용</para>
/// </summary>
[CreateAssetMenu(fileName = "New Stage Data", menuName = "BrickMan/Stage Data")]
public class StageData : ScriptableObject
{
    // 정의 필요

    [Header("기본 정보")]
    public string stageName;   // 예: "Tutorial Level"
    public int stageIndex;     // 예: 1
}