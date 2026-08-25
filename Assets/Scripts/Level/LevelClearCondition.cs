using UnityEngine;

//구본환 8.16 -> 8.19 클리어 조건 변경: 버그존 정화 -> NPC가 레벨 끝에 도착
/// <summary>
/// Unlocks the level portal once the NPC has reached the end of its checkpoint
/// chain. Add this to LevelManager (or any always-active object) and assign the
/// portal. The NPC progression controller is found automatically if left unassigned.
/// </summary>
public class LevelClearCondition : MonoBehaviour
{
    [SerializeField] private LevelPortal portal;
    [SerializeField] private NpcProgressionController npcProgression;

    private void Start()
    {
        if (npcProgression == null)
            npcProgression = FindFirstObjectByType<NpcProgressionController>();

        portal?.SetUnlocked(false);

        if (npcProgression == null)
        {
            Debug.LogWarning($"[{nameof(LevelClearCondition)}] No {nameof(NpcProgressionController)} in the scene; the portal will stay locked.");
            return;
        }

        npcProgression.ChainCompleted += OnChainCompleted;

        // In case the chain already finished before this Start ran.
        if (npcProgression.IsChainComplete)
            OnChainCompleted();
    }

    private void OnDestroy()
    {
        if (npcProgression != null)
            npcProgression.ChainCompleted -= OnChainCompleted;
    }

    private void OnChainCompleted()
    {
        portal?.SetUnlocked(true);
    }
}
