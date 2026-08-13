using UnityEngine;
using TMPro;
public class BugClean : MonoBehaviour
{
    [SerializeField] private BugZone bugZone;
    [SerializeField] private PlayerHideController playerHideController;
    [SerializeField] private float cleanseTime = 3f;
   
    private Vector3Int currentCell;
    private float timer;

    private void Update()
    {
        // 숨기 상태 확인
        if (!playerHideController.IsHidden)
        {
            timer = 0f;
            return; 
        }

        currentCell = playerHideController.GetCurrentHideCell();

        //오염된 타일이 아니면 시간 초기화 
        if (!bugZone.isInfected(currentCell))
        {
            timer = 0f; return;
        }
        // 맞으면 timer 증가

        timer += Time.deltaTime;
        Debug.Log(timer);

        if (timer>= cleanseTime)
        {
            bugZone.ClearInfection(currentCell);

            timer = 0f; 
        }
        // cleanseTime 이상이면 ClearInfection()
    }
}