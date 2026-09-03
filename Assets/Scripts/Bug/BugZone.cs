using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Why an infection was removed. Only <see cref="InfectionClearCause.Player"/> counts as
/// the player actually doing the work: it is what arms the recently-cleaned cooldown and
/// what can trigger Relief. Anything removed for some other reason (a system reset, a
/// future scripted effect, debug tooling) must use <see cref="InfectionClearCause.System"/>
/// so it cannot be mistaken for player progress.
/// </summary>
public enum InfectionClearCause
{
    System,
    Player
}

/// <summary>
/// A single map-wide bug zone. Bugs spawn in batches (see <see cref="BugSpawnDirector"/>),
/// biased toward where the NPC is heading next rather than uniformly at random. This class
/// owns the infection state - the tilemaps, which cells are infected, their ages, the Crash
/// Gauge contribution, the recently-cleaned cooldown and the Relief latch - and delegates
/// the "which cells" question entirely to the director.
///
/// Clearing bugs is not the level goal - the player just keeps the map clean while the NPC
/// works toward the end of the level.
/// </summary>
public class BugZone : MonoBehaviour
{
    [Header("Spawning")]
    [Tooltip("All spawn tuning: batch size/interval, hard filters, NPC route weighting, player modifier, spacing, relief strength and debug gizmos.")]
    [SerializeField] private BugSpawnDirector spawnDirector = new BugSpawnDirector();
    [Tooltip("Hard cap on how many infections can exist at once. A batch is trimmed to whatever headroom is left, and skipped entirely at the cap. 0 or less = no cap.")]
    [SerializeField] private int maxActiveInfections = 20;
    [Tooltip("Infections placed the instant the level starts, uniformly at random over the whole map. Ignores all spawn weighting, the player exclusion radius and spacing - npcSafeRadius is the only rule it respects. 0 = start the level clean.")]
    [SerializeField] private int initialRandomSpawnCount = 3;

    [Header("NPC Avoidance")]
    [Tooltip("Bugs never spawn on a tile whose center is within this world-space radius of an NPC.")]
    [SerializeField] private float npcSafeRadius = 5f;
    [Tooltip("NPCs to keep bug spawns away from. Found automatically if the list is left empty.")]
    [SerializeField] private NpcCommandPlayback[] npcs;

    [Header("Relief")]
    [Tooltip("World-space radius around an NPC that counts as its 'pressure area'. Relief triggers when the player empties this area, and during Relief candidates inside it are damped. Deliberately separate from npcSafeRadius and crashNpcProximityRadius.")]
    [SerializeField] private float npcPressureRadius = 7f;
    [Tooltip("How many infections must have built up inside the pressure area before clearing it counts as a real achievement. Below this, cleaning the area does not grant Relief.")]
    [SerializeField] private int reliefInfectionThreshold = 3;
    [Tooltip("Log to the console when Relief is earned, when it is consumed by a batch, and when the player clears the pressure area but the peak was below the threshold. Tuning aid - turn off for a quiet console.")]
    [SerializeField] private bool logReliefEvents = true;

    [Header("Crash Gauge")]
    [Tooltip("Crash Gauge manager to feed. Found automatically if left unassigned.")]
    [SerializeField] private CrashGaugeManager crashGaugeManager;
    [Tooltip("Seconds an infected tile must exist before it starts contributing to the Crash Gauge on its own.")]
    [SerializeField] private float bugAgeThreshold = 20f;
    [Tooltip("Crash Gauge points per second contributed by an infected tile once it is older than bugAgeThreshold.")]
    [SerializeField] private float agedBugCrashRate = 2f;
    [Tooltip("Crash Gauge points per second contributed by an infected tile while an NPC is within crashNpcProximityRadius of it, regardless of age.")]
    [SerializeField] private float npcNearbyCrashRate = 10f;
    [Tooltip("World-space radius used to decide whether an NPC is 'close' to an infected tile for Crash Gauge purposes. Deliberately separate from npcSafeRadius - spawn avoidance and Crash Gauge danger are different gameplay concepts and should be tunable independently.")]
    [SerializeField] private float crashNpcProximityRadius = 3f;

    [Header("Tilemaps")]
    // 감염 후보 타일맵 (숨을 수 있는 타일 전체가 스폰 후보)
    [SerializeField] private Tilemap targetTilemap;
    // 감염 표시용 오버레이 (레벨 타일맵보다 위에 그려지는 별도 타일맵) //구본환 8.15
    [SerializeField] private Tilemap infectionTilemap;
    // 흰색 스프라이트 타일이어야 틴트 색이 제대로 보임
    [SerializeField] private TileBase infectionTile;
    // 감염 표시 색 (알파를 낮춰 반투명 오버레이로 표시)
    [SerializeField] private Color infectionColor = new Color(1f, 0f, 1f, 0.35f);

    [Header("Player")]
    [Tooltip("Player transform used for the spawn exclusion radius and the player-distance modifier. Found by the 'Player' tag if left unassigned.")]
    [SerializeField] private Transform player;

    private readonly List<Vector3Int> spawnableCells = new List<Vector3Int>();
    private readonly List<Vector3Int> infectedTiles = new List<Vector3Int>();
    private readonly Dictionary<Vector3Int, float> infectionAge = new Dictionary<Vector3Int, float>();
    private readonly HashSet<Vector3Int> crashContributingCells = new HashSet<Vector3Int>();

    // Time.time at which the PLAYER last cleaned each cell. Drives the recently-cleaned
    // hard exclusion; pruned once per batch so it cannot grow unbounded.
    private readonly Dictionary<Vector3Int, float> playerCleanTimes = new Dictionary<Vector3Int, float>();

    private readonly List<NpcProgressionController> npcProgressions = new List<NpcProgressionController>();
    private readonly List<Vector3Int> batchBuffer = new List<Vector3Int>();
    private readonly List<Vector3Int> pruneBuffer = new List<Vector3Int>();

    private float spawnTimer;

    // Highest infection count seen inside the NPC pressure area since it was last empty.
    // Reset to 0 whenever the area empties for any reason other than a player clean, which
    // is exactly what stops "the NPC simply walked away" from earning Relief.
    private int pressurePeak;

    private void Start()
    {
        CollectSpawnableCells();

        if (npcs == null || npcs.Length == 0)
            npcs = FindObjectsByType<NpcCommandPlayback>(FindObjectsSortMode.None);

        CacheNpcProgressions();

        if (crashGaugeManager == null)
            crashGaugeManager = FindFirstObjectByType<CrashGaugeManager>();

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        SpawnInitialRandomInfections();

        spawnTimer = spawnDirector.FirstBatchDelay;
    }

    /// <summary>
    /// Seeds the level with a handful of infections the moment it starts, so the player
    /// has something to do before the first weighted batch arrives. Deliberately dumb:
    /// uniform random over every spawnable cell, with npcSafeRadius as the only rule.
    /// None of the director's weighting, player exclusion or spacing applies - this is
    /// starting scatter, not directed pressure.
    /// </summary>
    private void SpawnInitialRandomInfections()
    {
        int count = initialRandomSpawnCount;
        if (maxActiveInfections > 0)
            count = Mathf.Min(count, maxActiveInfections);

        if (count <= 0)
            return;

        // Copied so cells can be removed as they are used, which keeps the picks distinct
        // without re-rolling until a free one turns up.
        List<Vector3Int> candidates = new List<Vector3Int>();
        for (int i = 0; i < spawnableCells.Count; i++)
        {
            if (!IsWithinRadiusOfAnyNpc(spawnableCells[i], npcSafeRadius))
                candidates.Add(spawnableCells[i]);
        }

        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int index = Random.Range(0, candidates.Count);
            Infect(candidates[index]);
            candidates.RemoveAt(index);
        }
    }

    /// <summary>Resolved once, next to each playback, so the route forecast never has to
    /// GetComponent per batch. Null entries are fine - the forecast falls back to the
    /// NPC's current position.</summary>
    private void CacheNpcProgressions()
    {
        npcProgressions.Clear();

        if (npcs == null)
            return;

        for (int i = 0; i < npcs.Length; i++)
        {
            npcProgressions.Add(npcs[i] != null ? npcs[i].GetComponent<NpcProgressionController>() : null);
        }
    }

    private void Update()
    {
        AccumulateCrashGauge();
        UpdatePressureTracking();

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f)
            return;

        // Wait a full interval before retrying even when no valid tile was found
        // (e.g. the NPC is currently near every free tile), so we don't rescan
        // the map every frame.
        spawnTimer = spawnDirector.BatchInterval;

        SpawnBatch();
    }

    /// <summary>
    /// Ages every currently infected tile and feeds the Crash Gauge. Each tile
    /// contributes independently; if a tile is both past bugAgeThreshold and has an
    /// NPC within crashNpcProximityRadius, the higher of the two rates is used
    /// (not their sum). Also refreshes crashContributingCells, so callers like the
    /// minimap can tell which tiles are dangerous right now without recomputing the
    /// age/proximity checks themselves.
    /// </summary>
    private void AccumulateCrashGauge()
    {
        crashContributingCells.Clear();

        if (infectedTiles.Count == 0)
            return;

        float deltaTime = Time.deltaTime;

        for (int i = 0; i < infectedTiles.Count; i++)
        {
            Vector3Int cell = infectedTiles[i];

            float age = infectionAge.TryGetValue(cell, out float currentAge) ? currentAge + deltaTime : deltaTime;
            infectionAge[cell] = age;

            float rate = age >= bugAgeThreshold ? agedBugCrashRate : 0f;

            if (IsWithinRadiusOfAnyNpc(cell, crashNpcProximityRadius))
                rate = Mathf.Max(rate, npcNearbyCrashRate);

            if (rate > 0f)
            {
                crashContributingCells.Add(cell);
                crashGaugeManager?.Add(rate * deltaTime);
            }
        }
    }

    /// <summary>
    /// Tracks how bad the NPC's pressure area has gotten. The peak only survives while the
    /// area is non-empty: the instant it empties for any reason other than a player clean
    /// (typically the NPC walking away from its infections) the credit is thrown away, so
    /// a later single player clean cannot cash in someone else's work as Relief. The
    /// player-clean path in <see cref="ClearInfection"/> latches Relief before this runs.
    /// </summary>
    private void UpdatePressureTracking()
    {
        int count = CountPressureInfections();

        if (count == 0)
        {
            pressurePeak = 0;
            return;
        }

        pressurePeak = Mathf.Max(pressurePeak, count);
    }

    private int CountPressureInfections()
    {
        if (npcPressureRadius <= 0f)
            return 0;

        int count = 0;
        for (int i = 0; i < infectedTiles.Count; i++)
        {
            if (IsWithinRadiusOfAnyNpc(infectedTiles[i], npcPressureRadius))
                count++;
        }

        return count;
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

    /// <summary>
    /// Asks the director for this batch's cells and infects them. Relief is consumed only
    /// if the batch actually produced something, so a batch that found nothing valid does
    /// not silently burn the player's reward.
    /// </summary>
    private void SpawnBatch()
    {
        // At the cap, skip the batch entirely without touching Relief - the player keeps
        // the reward for the first batch that can actually use it.
        int headroom = maxActiveInfections > 0 ? maxActiveInfections - infectedTiles.Count : int.MaxValue;
        if (headroom <= 0)
            return;

        PrunePlayerCleanTimes();

        bool spawnedUnderRelief = ReliefPending;

        spawnDirector.SelectBatch(this, batchBuffer, headroom);

        for (int i = 0; i < batchBuffer.Count; i++)
        {
            Infect(batchBuffer[i]);
        }

        if (batchBuffer.Count > 0)
        {
            ReliefPending = false;

            if (spawnedUnderRelief && logReliefEvents)
            {
                Debug.Log($"[{nameof(BugZone)}] Relief consumed: batch of {batchBuffer.Count} spawned with near-NPC candidates damped. Back to normal weighting.", this);
            }
        }
    }

    private void Infect(Vector3Int cellPosition)
    {
        infectionTilemap.SetTile(cellPosition, infectionTile);
        infectionTilemap.SetTileFlags(cellPosition, TileFlags.None);
        infectionTilemap.SetColor(cellPosition, infectionColor);
        infectedTiles.Add(cellPosition);
        infectionAge[cellPosition] = 0f;
    }

    private void PrunePlayerCleanTimes()
    {
        float cooldown = spawnDirector.RecentCleanCooldown;

        pruneBuffer.Clear();
        foreach (KeyValuePair<Vector3Int, float> entry in playerCleanTimes)
        {
            if (Time.time - entry.Value >= cooldown)
                pruneBuffer.Add(entry.Key);
        }

        for (int i = 0; i < pruneBuffer.Count; i++)
        {
            playerCleanTimes.Remove(pruneBuffer[i]);
        }
    }

    /// <summary>Shared distance check used for spawn avoidance (npcSafeRadius), Crash Gauge
    /// danger (crashNpcProximityRadius) and Relief pressure (npcPressureRadius) - same
    /// logic, independently tunable radii, since those are separate gameplay concepts.</summary>
    internal bool IsWithinRadiusOfAnyNpc(Vector3Int cell, float radius)
    {
        return IsPointWithinRadiusOfAnyNpc(targetTilemap.GetCellCenterWorld(cell), radius);
    }

    /// <summary>World-position flavour of <see cref="IsWithinRadiusOfAnyNpc"/>, for callers
    /// that already have a cell center (or any other point) in hand.</summary>
    internal bool IsPointWithinRadiusOfAnyNpc(Vector2 worldPosition, float radius)
    {
        if (npcs == null || radius <= 0f)
            return false;

        float sqrRadius = radius * radius;

        for (int i = 0; i < npcs.Length; i++)
        {
            NpcCommandPlayback npc = npcs[i];
            if (npc == null)
                continue;

            // Rigidbody position is the physics-accurate one while playback moves the NPC.
            Vector2 npcPosition = npc.Body != null
                ? npc.Body.position
                : (Vector2)npc.transform.position;

            if ((npcPosition - worldPosition).sqrMagnitude <= sqrRadius)
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

    /// <summary>Infected cells whose effective Crash Gauge rate is currently greater
    /// than 0 (aged past bugAgeThreshold, and/or an NPC within crashNpcProximityRadius).
    /// Recomputed every Update alongside the gauge accumulation itself - callers (e.g.
    /// the minimap) should read this rather than re-deriving age/proximity state.</summary>
    public IReadOnlyCollection<Vector3Int> CrashContributingCells => crashContributingCells;

    /// <summary>Tilemap whose cells can get infected (e.g. so the minimap can draw
    /// the hideable layer without a manual reference).</summary>
    public Tilemap TargetTilemap => targetTilemap;

    /// <summary>Every painted cell of the target tilemap, collected once at Start.</summary>
    internal IReadOnlyList<Vector3Int> SpawnableCells => spawnableCells;

    internal IReadOnlyList<NpcCommandPlayback> NpcPlaybacks => npcs;

    /// <summary>Progression controllers parallel to <see cref="NpcPlaybacks"/> (entries may
    /// be null), used by the route forecast to look ahead along the checkpoint chain.</summary>
    internal IReadOnlyList<NpcProgressionController> NpcProgressions => npcProgressions;

    internal float NpcSafeRadius => npcSafeRadius;
    internal float NpcPressureRadius => npcPressureRadius;

    /// <summary>True for exactly one upcoming batch after the player empties the NPC's
    /// pressure area. Consumed by the batch that spawns under it.</summary>
    internal bool ReliefPending { get; private set; }

    /// <summary>True while this cell is still inside its post-clean cooldown. Only player
    /// cleans arm this - a system removal leaves the cell immediately re-infectable.</summary>
    internal bool WasCleanedWithin(Vector3Int cell, float seconds)
    {
        if (seconds <= 0f)
            return false;

        return playerCleanTimes.TryGetValue(cell, out float cleanedAt) && Time.time - cleanedAt < seconds;
    }

    internal bool TryGetPlayerPosition(out Vector2 position)
    {
        if (player == null)
        {
            position = Vector2.zero;
            return false;
        }

        position = player.position;
        return true;
    }

    /// <summary>World-space center of a cell on this zone's tilemap, for LOS/distance
    /// checks against a tile rather than a collider.</summary>
    public Vector3 GetCellWorldCenter(Vector3Int cell)
    {
        return targetTilemap != null ? targetTilemap.GetCellCenterWorld(cell) : (Vector3)cell;
    }

    /// <summary>
    /// Removes an infection. <paramref name="cause"/> decides whether this counts as player
    /// progress: a player clean arms the recently-cleaned cooldown for that cell and can
    /// trigger Relief, a system removal does neither.
    /// </summary>
    public void ClearInfection(Vector3Int cell, InfectionClearCause cause = InfectionClearCause.System)
    {
        if (!infectedTiles.Contains(cell))
        {
            return; //아무것도 없음
        }

        bool wasInPressureArea = IsWithinRadiusOfAnyNpc(cell, npcPressureRadius);

        infectionTilemap.SetTile(cell, null); //구본환 8.15

        infectedTiles.Remove(cell);
        infectionAge.Remove(cell);

        if (cause != InfectionClearCause.Player)
            return;

        playerCleanTimes[cell] = Time.time;

        if (wasInPressureArea)
            TryTriggerRelief();
    }

    /// <summary>
    /// Relief is earned only when all three hold: the pressure area had built up to at
    /// least reliefInfectionThreshold infections, the PLAYER did the cleaning, and that
    /// clean is what took the area from >0 to 0. The peak is consumed either way, so the
    /// next Relief has to be earned from scratch.
    /// </summary>
    private void TryTriggerRelief()
    {
        int remaining = CountPressureInfections();

        // Not the last one - the area is not clear yet, so there is nothing to report.
        if (remaining > 0)
            return;

        if (pressurePeak < reliefInfectionThreshold)
        {
            // The area is clear but never got bad enough to count. Worth logging: this is
            // the case that looks like "Relief should have fired but didn't".
            if (logReliefEvents)
            {
                Debug.Log($"[{nameof(BugZone)}] NPC pressure area cleared by the player, but no Relief: peak was {pressurePeak} infection(s), threshold is {reliefInfectionThreshold}.", this);
            }

            return;
        }

        if (logReliefEvents)
        {
            Debug.Log($"[{nameof(BugZone)}] Relief earned: player cleared the NPC pressure area (peak {pressurePeak} infection(s) >= threshold {reliefInfectionThreshold}). The next batch that spawns will damp near-NPC candidates.", this);
        }

        ReliefPending = true;
        pressurePeak = 0;
    }

    public void ClearInfectionInRange(Vector3 center, float range, InfectionClearCause cause = InfectionClearCause.System)
    {
        float sqrRange = range * range;

        // ClearInfection()에서 infectedTiles가 삭제되므로 뒤에서부터 순회
        for (int i = infectedTiles.Count - 1; i >= 0; i--)
        {
            Vector3Int cell = infectedTiles[i];
            Vector3 cellCenter = targetTilemap.GetCellCenterWorld(cell);

            if ((cellCenter - center).sqrMagnitude <= sqrRange)
            {
                ClearInfection(cell, cause);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        spawnDirector?.DrawGizmos(this);
    }
}
