using UnityEngine;
using TMPro;
public class BugClean : MonoBehaviour
{
    [SerializeField] private BugZone bugZone;
    [SerializeField] private PlayerHideController playerHideController;
    [SerializeField] private StatusEffectController statusEffects;
    [SerializeField] private float cleanseTime = 3f;

    [Header("Progress Bar")]
    [SerializeField] private Vector3 progressBarOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Vector2 progressBarSize = new Vector2(1f, 0.15f);
    [SerializeField] private Color progressBarBackground = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color progressBarFill = new Color(0.3f, 1f, 0.3f, 1f);

    private Vector3Int currentCell;
    private float timer;
    private WorldSpaceProgressBar progressBar;

    private void Awake()
    {
        // Not parented to the player: the tile being cleaned can be up to bugSnapRange
        // away (auto-targeted), not necessarily right under the player, so the bar
        // tracks the bug tile's own world position instead (see Update).
        progressBar = WorldSpaceProgressBar.Create(null, Vector3.zero, progressBarSize, progressBarBackground, progressBarFill);
        if (statusEffects == null && playerHideController != null)
        {
            statusEffects = playerHideController.GetComponent<StatusEffectController>();
        }
    }

    private void Update()
    {
        // 숨기 상태 확인
        if (!playerHideController.IsHidden)
        {
            timer = 0f;
            progressBar.SetVisible(false);
            return;
        }

        currentCell = playerHideController.GetCurrentHideCell();

        //오염된 타일이 아니면 시간 초기화
        if (!bugZone.isInfected(currentCell))
        {
            timer = 0f;
            progressBar.SetVisible(false);
            return;
        }
        // 맞으면 timer 증가

        timer += Time.deltaTime;
        progressBar.transform.position = bugZone.GetCellWorldCenter(currentCell) + progressBarOffset;
        progressBar.SetVisible(true);
        float effectiveCleanseTime = GetEffectiveCleanseTime();
        progressBar.SetFill(timer / effectiveCleanseTime);

        if (timer >= effectiveCleanseTime)
        {
            // The player personally stood on this tile for the full cleanse time, so this
            // arms the recently-cleaned cooldown and can earn Relief.
            bugZone.ClearInfection(currentCell, InfectionClearCause.Player);

            timer = 0f;
            progressBar.SetVisible(false);
        }
        // cleanseTime 이상이면 ClearInfection()
    }

    private float GetEffectiveCleanseTime()
    {
        float reduction = statusEffects != null
            ? statusEffects.GetValue(StatusEffectType.BugCooldown)
            : 0f;

        return Mathf.Max(0.1f, cleanseTime - reduction);
    }
}
