using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A single map-wide bug zone. Bugs spawn endlessly, one at a time at a fixed
/// interval, on random tiles of the target tilemap, skipping tiles near an NPC.
/// Clearing bugs is no longer the level goal - the player just keeps the map
/// clean while the NPC works toward the end of the level.
/// </summary>
public class BugZone : MonoBehaviour
{
    [Header("Spawning")]
    [Tooltip("Seconds between bug spawns. The first bug spawns immediately.")]
    [SerializeField] private float spawnInterval = 5f;

    [Header("NPC Avoidance")]
    [Tooltip("Bugs never spawn on a tile whose center is within this world-space radius of an NPC.")]
    [SerializeField] private float npcSafeRadius = 5f;
    [Tooltip("NPCs to keep bug spawns away from. Found automatically if the list is left empty.")]
    [SerializeField] private NpcCommandPlayback[] npcs;

    [Header("Tilemaps")]
    // 감염 후보 타일맵 (숨을 수 있는 타일 전체가 스폰 후보)
    [SerializeField] private Tilemap targetTilemap;
    // 감염 표시용 오버레이 (레벨 타일맵보다 위에 그려지는 별도 타일맵) //구본환 8.15
    [SerializeField] private Tilemap infectionTilemap;
    // 흰색 스프라이트 타일이어야 틴트 색이 제대로 보임
    [SerializeField] private TileBase infectionTile;
    // 감염 표시 색 (알파를 낮춰 반투명 오버레이로 표시)
    [SerializeField] private Color infectionColor = new Color(1f, 0f, 1f, 0.35f);

    private readonly List<Vector3Int> spawnableCells = new List<Vector3Int>();
    private readonly List<Vector3Int> infectedTiles = new List<Vector3Int>();
    private float spawnTimer;

    private void Start()
    {
        CollectSpawnableCells();

        if (npcs == null || npcs.Length == 0)
            npcs = FindObjectsByType<NpcCommandPlayback>(FindObjectsSortMode.None);

        // First bug spawns on the first Update tick, the rest every spawnInterval.
        spawnTimer = 0f;
    }

    private void Update()
    {
        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f)
            return;

        // Wait a full interval before retrying even when no valid tile was found
        // (e.g. the NPC is currently near every free tile), so we don't rescan
        // the map every frame.
        spawnTimer = spawnInterval;

        TrySpawnBug();
    }

    /// <summary>The whole target tilemap is the zone: every painted cell is a spawn candidate.</summary>
    private void CollectSpawnableCells()
    {
        spawnableCells.Clear();

        foreach (Vector3Int cell in targetTilemap.cellBounds.allPositionsWithin)
        {
            if (targetTilemap.HasTile(cell))
                spawnableCells.Add(cell);
        }
    }

    private bool TrySpawnBug()
    {
        List<Vector3Int> candidates = new List<Vector3Int>();

        for (int i = 0; i < spawnableCells.Count; i++)
        {
            Vector3Int cell = spawnableCells[i];

            if (infectedTiles.Contains(cell))
                continue;
            if (IsNearNpc(cell))
                continue;

            candidates.Add(cell);
        }

        if (candidates.Count == 0)
            return false;

        Vector3Int cellPosition = candidates[Random.Range(0, candidates.Count)];

        infectionTilemap.SetTile(cellPosition, infectionTile);
        infectionTilemap.SetTileFlags(cellPosition, TileFlags.None);
        infectionTilemap.SetColor(cellPosition, infectionColor);
        infectedTiles.Add(cellPosition);

        return true;
    }

    private bool IsNearNpc(Vector3Int cell)
    {
        if (npcs == null || npcSafeRadius <= 0f)
            return false;

        Vector2 cellCenter = targetTilemap.GetCellCenterWorld(cell);
        float sqrSafeRadius = npcSafeRadius * npcSafeRadius;

        for (int i = 0; i < npcs.Length; i++)
        {
            NpcCommandPlayback npc = npcs[i];
            if (npc == null)
                continue;

            // Rigidbody position is the physics-accurate one while playback moves the NPC.
            Vector2 npcPosition = npc.Body != null
                ? npc.Body.position
                : (Vector2)npc.transform.position;

            if ((npcPosition - cellCenter).sqrMagnitude <= sqrSafeRadius)
                return true;
        }

        return false;
    }

    public bool isInfected(Vector3Int cell)
    {
        return infectedTiles.Contains(cell);
    }

    /// <summary>Currently infected cells, for callers (e.g. NpcVisionSensor) that need
    /// to check each one rather than a single cell.</summary>
    public IReadOnlyList<Vector3Int> InfectedCells => infectedTiles;

    /// <summary>World-space center of a cell on this zone's tilemap, for LOS/distance
    /// checks against a tile rather than a collider.</summary>
    public Vector3 GetCellWorldCenter(Vector3Int cell)
    {
        return targetTilemap != null ? targetTilemap.GetCellCenterWorld(cell) : (Vector3)cell;
    }

    public void ClearInfection(Vector3Int cell)
    {
        if (!infectedTiles.Contains(cell))
        {
            return; //아무것도 없음 
        }

        infectionTilemap.SetTile(cell, null); //구본환 8.15

        infectedTiles.Remove(cell);
    }
}
