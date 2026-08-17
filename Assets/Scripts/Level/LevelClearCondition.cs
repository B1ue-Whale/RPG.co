using UnityEngine;

//구본환 8.16 모든 BugZone 정화 시 포탈 해금
/// <summary>
/// Unlocks the level portal once every BugZone in the scene has been cleansed.
/// Add this to LevelManager (or any always-active object) and assign the portal.
/// Bug zones are found automatically if the list is left empty.
/// </summary>
public class LevelClearCondition : MonoBehaviour
{
    [SerializeField] private LevelPortal portal;
    [SerializeField] private BugZone[] bugZones;

    private void Start()
    {
        if (bugZones == null || bugZones.Length == 0)
            bugZones = FindObjectsByType<BugZone>(FindObjectsSortMode.None);

        portal?.SetUnlocked(false);

        for (int i = 0; i < bugZones.Length; i++)
        {
            if (bugZones[i] == null)
                continue;
            bugZones[i].Cleared += OnZoneCleared;
        }

        TryUnlockPortal();
    }

    private void OnDestroy()
    {
        if (bugZones == null)
            return;

        for (int i = 0; i < bugZones.Length; i++)
        {
            if (bugZones[i] == null)
                continue;
            bugZones[i].Cleared -= OnZoneCleared;
        }
    }

    private void OnZoneCleared(BugZone zone)
    {
        TryUnlockPortal();
    }

    private void TryUnlockPortal()
    {
        for (int i = 0; i < bugZones.Length; i++)
        {
            if (bugZones[i] != null && !bugZones[i].IsCleared)
                return;
        }

        portal?.SetUnlocked(true);
    }
}
