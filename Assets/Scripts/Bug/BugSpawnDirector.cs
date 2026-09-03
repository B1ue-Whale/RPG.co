using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chooses WHERE bugs spawn. Owns every spawn-tuning value and all of the filtering,
/// weighting and weighted-random selection; <see cref="BugZone"/> keeps ownership of the
/// infection state itself (tilemaps, ages, Crash Gauge, Relief bookkeeping) and just asks
/// this for a batch of cells.
///
/// Serialized as a plain [System.Serializable] field on BugZone rather than a separate
/// component, so all of the tuning still shows up in the BugZone Inspector while the
/// selection logic stays out of BugZone.cs.
///
/// Final weight of a surviving candidate:
///     npcRelevance * playerModifier * sameBatchSpacingModifier * reliefModifier
/// npcRelevance spans roughly 0.05..1.0 while the player modifier is bounded around 1.0
/// (~0.8..1.1), so NPC route relevance always dominates and player distance can only
/// reorder near-ties - which is the intended design.
/// </summary>
[System.Serializable]
public class BugSpawnDirector
{
    [Header("Batch")]
    [Tooltip("Seconds between spawn batches.")]
    [SerializeField] private float batchInterval = 10f;
    [Tooltip("Seconds before the very first batch of the level.")]
    [SerializeField] private float firstBatchDelay = 3f;
    [Tooltip("Minimum bugs spawned in one batch (inclusive).")]
    [SerializeField] private int minBugsPerBatch = 2;
    [Tooltip("Maximum bugs spawned in one batch (inclusive).")]
    [SerializeField] private int maxBugsPerBatch = 3;

    [Header("Hard Filters")]
    [Tooltip("Bugs never spawn within this world-space radius of the player. Hard exclusion, never relaxed.")]
    [SerializeField] private float playerExclusionRadius = 2.5f;
    [Tooltip("Seconds after the PLAYER cleans a cell during which that cell cannot be re-infected. Hard exclusion, never relaxed - a batch spawns fewer bugs rather than immediately re-infecting what was just cleaned.")]
    [SerializeField] private float recentCleanCooldown = 20f;

    [Header("NPC Relevance (primary weight)")]
    [Tooltip("Weight by estimated seconds until the NPC's route reaches this area. Default: immediate = low (the player needs reaction time), a few seconds out = peak, far future = moderate.")]
    [SerializeField] private AnimationCurve etaWeightCurve = new AnimationCurve(
        new Keyframe(0f, 0.15f), new Keyframe(3f, 1f), new Keyframe(8f, 0.6f), new Keyframe(20f, 0.3f));
    [Tooltip("How far off the forecast route a cell may be and still count as 'on the NPC's way'. Should be generous: the forecast is a straight line between checkpoints, not the real walked path.")]
    [SerializeField] private float routeCorridorRadius = 6f;
    [Tooltip("Weight falloff across the corridor. X = distance from the route / routeCorridorRadius, Y = multiplier.")]
    [SerializeField] private AnimationCurve corridorFalloff = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(1f, 0.25f));
    [Tooltip("Spacing (world units) of the sampled points along the forecast route.")]
    [SerializeField] private float routeSampleSpacing = 1.5f;
    [Tooltip("Weight for cells outside the corridor entirely - behind the NPC, or off its remaining route. Keep small but non-zero so distant areas are still rarely used.")]
    [SerializeField] private float offRouteWeight = 0.05f;
    [Tooltip("How many checkpoints ahead the forecast looks. 1 = only the checkpoint the NPC is currently walking toward.")]
    [SerializeField] private int routeLookaheadCheckpoints = 3;

    [Header("Player Distance (small secondary modifier)")]
    [Tooltip("X = distance from the player in world units, Y = 0..1 normalized preference, remapped into [playerModMin, playerModMax].")]
    [SerializeField] private AnimationCurve playerDistanceCurve = new AnimationCurve(
        new Keyframe(0f, 0.6f), new Keyframe(6f, 1f), new Keyframe(18f, 1f), new Keyframe(40f, 0f));
    [Tooltip("Multiplier at normalized preference 0 (very far / awkward).")]
    [SerializeField] private float playerModMin = 0.8f;
    [Tooltip("Multiplier at normalized preference 1 (comfortably reachable).")]
    [SerializeField] private float playerModMax = 1.1f;
    [Tooltip("Global strength of the whole player-distance effect. 0 = ignore the player entirely (apart from the hard exclusion radius), 1 = full curve.")]
    [Range(0f, 1f)]
    [SerializeField] private float playerWeightInfluence = 1f;

    [Header("Same-Batch Spacing")]
    // Deliberately scoped to the current batch only. Existing infections do NOT push
    // spawns away - clusters around the NPC's route are allowed to build up, and NPC
    // relevance is never weakened just because that area is already infected. This only
    // stops the 2-3 bugs of a single batch from landing on top of each other.
    [Tooltip("Cells within this many cells (Chebyshev distance) of a cell already chosen earlier in the SAME batch are rejected. 0 = allow direct adjacency within a batch. This is the ONLY filter relaxed when a batch would otherwise find nothing.")]
    [SerializeField] private int adjacencyExclusionCells = 1;
    [Tooltip("World radius used to count cells already chosen in this batch.")]
    [SerializeField] private float spacingRadius = 3f;
    [Tooltip("Multiplier applied once per cell already chosen nearby in this batch. 0.5 halves the weight for each one.")]
    [SerializeField] private float spacingPenaltyPerNeighbor = 0.5f;
    [Tooltip("Lower bound of the same-batch spacing penalty, so the effect stays mild rather than hard-forbidding a second bug in the same region.")]
    [SerializeField] private float spacingModifierFloor = 0.2f;

    [Header("Relief")]
    [Tooltip("Multiplier applied for ONE batch after the player fully cleans the NPC pressure area, to candidates that are immediately around the NPC (see reliefEtaThreshold and BugZone.npcPressureRadius). Farther future-route candidates keep their normal weight, so the batch still follows the NPC's route - it just lands further ahead.")]
    [SerializeField] private float reliefModifier = 0.2f;
    [Tooltip("During Relief, candidates whose route ETA is below this many seconds count as 'immediate' and get reliefModifier.")]
    [SerializeField] private float reliefEtaThreshold = 4f;

    [Header("Debug")]
    [Tooltip("Draw the last evaluated candidate weights as gizmos while the BugZone is selected. Blue = low weight, red = high, white wire cubes = the cells actually chosen. Play mode only - the weights depend on live NPC/player positions.")]
    [SerializeField] private bool drawSpawnWeightGizmos = false;
    [Tooltip("Re-evaluate the weights every frame instead of showing the snapshot from the last batch, so the gizmos follow the NPC and player live while tuning. Display only - it never spawns anything or consumes the Relief latch. Costs a full candidate scan per frame, so leave it off outside of tuning.")]
    [SerializeField] private bool liveGizmoPreview = false;
    [Tooltip("Size multiplier for the weight gizmo cubes.")]
    [SerializeField] private float gizmoWeightScale = 0.9f;

    public float BatchInterval => Mathf.Max(0.1f, batchInterval);
    public float FirstBatchDelay => Mathf.Max(0f, firstBatchDelay);
    public float RecentCleanCooldown => Mathf.Max(0f, recentCleanCooldown);

    private readonly List<NpcRouteForecast> _forecasts = new List<NpcRouteForecast>();

    // Reused per pick so selection does not allocate every batch.
    private readonly List<Vector3Int> _candidates = new List<Vector3Int>();
    private readonly List<float> _weights = new List<float>();
    private readonly List<Vector3Int> _occupiedCells = new List<Vector3Int>();
    private readonly List<Vector2> _occupiedCenters = new List<Vector2>();
    private readonly List<Vector3Int> _lastChosen = new List<Vector3Int>();
    // Stands in for "no cells chosen yet" while previewing, so the preview shows the state
    // a batch's first pick would see.
    private readonly List<Vector3Int> _previewEmptyBatch = new List<Vector3Int>();

    /// <summary>
    /// Picks a whole batch. Each cell is selected sequentially and the weights are
    /// recalculated in between, with the cells already chosen in this batch treated
    /// exactly like existing infections - so one batch never piles onto one spot.
    /// </summary>
    /// <param name="maxCount">Upper bound on this batch's size, e.g. the headroom left
    /// under BugZone's active-infection cap. The batch can still come out smaller.</param>
    public void SelectBatch(BugZone zone, List<Vector3Int> results, int maxCount = int.MaxValue)
    {
        results.Clear();
        _lastChosen.Clear();

        if (zone == null)
        {
            return;
        }

        EnsureCurves();
        RebuildForecasts(zone);

        int low = Mathf.Min(minBugsPerBatch, maxBugsPerBatch);
        int high = Mathf.Max(minBugsPerBatch, maxBugsPerBatch);
        int count = Mathf.Clamp(Random.Range(low, high + 1), 0, Mathf.Max(0, maxCount));

        for (int i = 0; i < count; i++)
        {
            if (!TryPickOne(zone, results, out Vector3Int cell))
            {
                // Nothing valid left (e.g. everything nearby is infected, cooling down
                // after a clean, or inside a safe radius). Spawn fewer bugs rather than
                // forcing a bad placement.
                break;
            }

            results.Add(cell);
            _lastChosen.Add(cell);
        }
    }

    /// <summary>
    /// A curve that ended up with no keys (e.g. this director was added to a component
    /// that was serialized before these fields existed) evaluates to a constant 0, which
    /// would silently flatten the whole weighting. Restore the defaults instead.
    /// </summary>
    private void EnsureCurves()
    {
        if (etaWeightCurve == null || etaWeightCurve.length == 0)
        {
            etaWeightCurve = new AnimationCurve(
                new Keyframe(0f, 0.15f), new Keyframe(3f, 1f), new Keyframe(8f, 0.6f), new Keyframe(20f, 0.3f));
        }

        if (corridorFalloff == null || corridorFalloff.length == 0)
        {
            corridorFalloff = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.25f));
        }

        if (playerDistanceCurve == null || playerDistanceCurve.length == 0)
        {
            playerDistanceCurve = new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(6f, 1f), new Keyframe(18f, 1f), new Keyframe(40f, 0f));
        }
    }

    private void RebuildForecasts(BugZone zone)
    {
        IReadOnlyList<NpcCommandPlayback> npcs = zone.NpcPlaybacks;
        IReadOnlyList<NpcProgressionController> progressions = zone.NpcProgressions;

        _forecasts.Clear();

        if (npcs == null)
        {
            return;
        }

        for (int i = 0; i < npcs.Count; i++)
        {
            if (npcs[i] == null)
            {
                continue;
            }

            NpcRouteForecast forecast = new NpcRouteForecast();
            NpcProgressionController progression = progressions != null && i < progressions.Count ? progressions[i] : null;
            forecast.Rebuild(npcs[i], progression, routeLookaheadCheckpoints, routeSampleSpacing);
            _forecasts.Add(forecast);
        }
    }

    private bool TryPickOne(BugZone zone, List<Vector3Int> batchSoFar, out Vector3Int chosen)
    {
        CollectOccupied(zone, batchSoFar);

        BuildCandidates(zone, allowAdjacent: false);
        if (_candidates.Count == 0)
        {
            // Only the spatial clustering rule is relaxed. Already-infected, NPC safe
            // radius, player exclusion and the recent-clean cooldown stay hard.
            BuildCandidates(zone, allowAdjacent: true);
        }

        if (_candidates.Count == 0)
        {
            chosen = default;
            return false;
        }

        float total = 0f;
        for (int i = 0; i < _weights.Count; i++)
        {
            total += _weights[i];
        }

        if (total <= 0f)
        {
            chosen = _candidates[Random.Range(0, _candidates.Count)];
            return true;
        }

        float roll = Random.value * total;
        for (int i = 0; i < _candidates.Count; i++)
        {
            roll -= _weights[i];
            if (roll <= 0f)
            {
                chosen = _candidates[i];
                return true;
            }
        }

        chosen = _candidates[_candidates.Count - 1];
        return true;
    }

    /// <summary>
    /// Only the cells already chosen in this batch. Existing infections are deliberately
    /// NOT included: an already-infected cell is still an invalid spawn target (the
    /// isInfected filter handles that), but it neither blocks its neighbours nor reduces
    /// their weight, so bugs may freely accumulate around wherever the NPC is going.
    /// </summary>
    private void CollectOccupied(BugZone zone, List<Vector3Int> batchSoFar)
    {
        _occupiedCells.Clear();
        _occupiedCenters.Clear();

        for (int i = 0; i < batchSoFar.Count; i++)
        {
            _occupiedCells.Add(batchSoFar[i]);
            _occupiedCenters.Add(zone.GetCellWorldCenter(batchSoFar[i]));
        }
    }

    private void BuildCandidates(BugZone zone, bool allowAdjacent)
    {
        _candidates.Clear();
        _weights.Clear();

        IReadOnlyList<Vector3Int> cells = zone.SpawnableCells;
        bool hasPlayer = zone.TryGetPlayerPosition(out Vector2 playerPosition);
        float sqrPlayerExclusion = playerExclusionRadius * playerExclusionRadius;
        bool reliefPending = zone.ReliefPending;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int cell = cells[i];

            if (zone.isInfected(cell))
                continue;
            if (zone.WasCleanedWithin(cell, RecentCleanCooldown))
                continue;
            if (zone.IsWithinRadiusOfAnyNpc(cell, zone.NpcSafeRadius))
                continue;

            Vector2 center = zone.GetCellWorldCenter(cell);

            if (hasPlayer && (center - playerPosition).sqrMagnitude <= sqrPlayerExclusion)
                continue;

            if (!TryGetSpacingModifier(cell, center, allowAdjacent, out float spacingModifier))
                continue;

            float npcRelevance = EvaluateNpcRelevance(center, out float eta);
            float weight = npcRelevance
                * EvaluatePlayerModifier(center, hasPlayer, playerPosition)
                * spacingModifier
                * EvaluateReliefModifier(zone, reliefPending, center, eta);

            if (weight <= 0f)
                continue;

            _candidates.Add(cell);
            _weights.Add(weight);
        }
    }

    /// <summary>
    /// Combined same-batch adjacency exclusion (hard, unless relaxed) and same-batch
    /// neighbour-count penalty (soft), in one pass. Always passes for the first pick of a
    /// batch, since nothing has been chosen yet.
    /// </summary>
    private bool TryGetSpacingModifier(Vector3Int cell, Vector2 center, bool allowAdjacent, out float modifier)
    {
        modifier = 1f;

        float sqrSpacing = spacingRadius * spacingRadius;
        int neighbors = 0;

        for (int i = 0; i < _occupiedCells.Count; i++)
        {
            Vector3Int other = _occupiedCells[i];

            if (!allowAdjacent && adjacencyExclusionCells >= 0)
            {
                int chebyshev = Mathf.Max(Mathf.Abs(other.x - cell.x), Mathf.Abs(other.y - cell.y));
                if (chebyshev <= adjacencyExclusionCells)
                {
                    return false;
                }
            }

            if ((_occupiedCenters[i] - center).sqrMagnitude <= sqrSpacing)
            {
                neighbors++;
            }
        }

        if (neighbors > 0)
        {
            modifier = Mathf.Max(spacingModifierFloor, Mathf.Pow(Mathf.Clamp01(spacingPenaltyPerNeighbor), neighbors));
        }

        return true;
    }

    /// <summary>
    /// Primary weight. Uses the best (highest) relevance across all NPCs, and reports the
    /// ETA of whichever forecast produced it, so Relief can tell "right in front of the
    /// NPC" apart from "further along its route".
    /// </summary>
    private float EvaluateNpcRelevance(Vector2 center, out float eta)
    {
        float best = offRouteWeight;
        float bestEta = 0f;
        bool any = false;

        for (int i = 0; i < _forecasts.Count; i++)
        {
            if (!_forecasts[i].TryGetClosest(center, out float distance, out float sampleEta))
            {
                continue;
            }

            float weight;
            if (distance > routeCorridorRadius)
            {
                weight = offRouteWeight;
            }
            else
            {
                float normalized = routeCorridorRadius > 0f ? distance / routeCorridorRadius : 0f;
                weight = Mathf.Max(offRouteWeight, etaWeightCurve.Evaluate(sampleEta) * corridorFalloff.Evaluate(normalized));
            }

            if (!any || weight > best)
            {
                best = weight;
                bestEta = sampleEta;
                any = true;
            }
        }

        eta = bestEta;
        return Mathf.Max(0f, best);
    }

    private float EvaluatePlayerModifier(Vector2 center, bool hasPlayer, Vector2 playerPosition)
    {
        if (!hasPlayer || playerWeightInfluence <= 0f)
        {
            return 1f;
        }

        float distance = Vector2.Distance(center, playerPosition);
        float normalized = Mathf.Clamp01(playerDistanceCurve.Evaluate(distance));
        float raw = Mathf.Lerp(playerModMin, playerModMax, normalized);

        return Mathf.Lerp(1f, raw, playerWeightInfluence);
    }

    /// <summary>
    /// Relief option A: only the candidates immediately around the NPC are damped - by
    /// ETA, or by sitting inside the pressure radius the player just cleaned. Everything
    /// further along the route keeps its normal weight, so the batch still follows the
    /// NPC's route; it just tends to land ahead of it instead of on top of it.
    /// </summary>
    private float EvaluateReliefModifier(BugZone zone, bool reliefPending, Vector2 center, float eta)
    {
        if (!reliefPending)
        {
            return 1f;
        }

        bool immediate = eta <= reliefEtaThreshold || zone.IsPointWithinRadiusOfAnyNpc(center, zone.NpcPressureRadius);
        return immediate ? Mathf.Max(0f, reliefModifier) : 1f;
    }

    /// <summary>
    /// Recomputes the candidate weights purely for visualization, as if a fresh batch were
    /// about to pick its first cell. Deliberately read-only with respect to game state: it
    /// does not spawn anything or consume Relief - the Relief latch is only read, exactly
    /// as a real pick would read it.
    /// </summary>
    private void RefreshPreview(BugZone zone)
    {
        EnsureCurves();
        RebuildForecasts(zone);

        _previewEmptyBatch.Clear();
        CollectOccupied(zone, _previewEmptyBatch);

        BuildCandidates(zone, allowAdjacent: false);
        if (_candidates.Count == 0)
        {
            BuildCandidates(zone, allowAdjacent: true);
        }
    }

    /// <summary>
    /// Draws the candidate weights - the snapshot from the most recent pick, or a live
    /// re-evaluation when liveGizmoPreview is on. Blue = low, red = high; white wire cubes
    /// mark the cells the last batch actually chose.
    /// </summary>
    public void DrawGizmos(BugZone zone)
    {
        if (!drawSpawnWeightGizmos || zone == null)
        {
            return;
        }

        if (liveGizmoPreview && Application.isPlaying)
        {
            RefreshPreview(zone);
        }

        if (_candidates.Count == 0)
        {
            return;
        }

        float max = 0f;
        for (int i = 0; i < _weights.Count; i++)
        {
            max = Mathf.Max(max, _weights[i]);
        }

        if (max <= 0f)
        {
            return;
        }

        Vector3 cellSize = zone.TargetTilemap != null ? zone.TargetTilemap.cellSize : Vector3.one;

        for (int i = 0; i < _candidates.Count; i++)
        {
            float t = _weights[i] / max;
            Gizmos.color = Color.Lerp(new Color(0.1f, 0.3f, 1f, 0.2f), new Color(1f, 0.2f, 0f, 0.9f), t);
            float size = gizmoWeightScale * Mathf.Lerp(0.15f, 1f, t);
            Gizmos.DrawCube(zone.GetCellWorldCenter(_candidates[i]), new Vector3(cellSize.x * size, cellSize.y * size, 0.01f));
        }

        Gizmos.color = Color.white;
        for (int i = 0; i < _lastChosen.Count; i++)
        {
            Gizmos.DrawWireCube(zone.GetCellWorldCenter(_lastChosen[i]), new Vector3(cellSize.x, cellSize.y, 0.01f));
        }
    }
}
