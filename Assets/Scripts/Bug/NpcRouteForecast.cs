using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A cheap estimate of where an NPC is going to be in the near future, expressed as a
/// polyline of world positions each tagged with an ETA in seconds.
///
/// Recordings store only <see cref="MotorCommand"/> (intent), never positions, so there
/// is no recorded path to read back. Instead this reconstructs a coarse route skeleton
/// from what the progression system already knows:
///   sample 0            = the NPC's actual current position          (eta 0)
///   next checkpoint     = remaining ticks of the current recording   (eta = ticks * fixedDeltaTime)
///   further checkpoints = average recording length of each segment
/// and then walks that polyline at a fixed spacing, interpolating the ETA.
///
/// This is a straight-line approximation of a platformer path, so it describes the
/// REGION the NPC is heading into, not the exact route it will walk. That is all the
/// spawn weighting needs. If the progression controller is missing, not running, or the
/// chain is already complete, the forecast degrades to a single sample at the NPC's
/// current position - i.e. plain "distance to the NPC right now" weighting - and the
/// scoring code above it does not change at all.
/// </summary>
public class NpcRouteForecast
{
    public struct Sample
    {
        public Vector2 position;
        public float eta;
    }

    private const int MaxSamples = 256;

    private readonly List<Sample> _samples = new List<Sample>();
    private readonly List<Sample> _waypoints = new List<Sample>();

    public IReadOnlyList<Sample> Samples => _samples;

    /// <summary>True when actual future checkpoints were available, false when this
    /// fell back to "the NPC's current position only".</summary>
    public bool HasRoute { get; private set; }

    public void Rebuild(NpcCommandPlayback playback, NpcProgressionController progression, int lookaheadCheckpoints, float sampleSpacing)
    {
        _samples.Clear();
        _waypoints.Clear();
        HasRoute = false;

        if (playback == null)
        {
            return;
        }

        Vector2 origin = playback.Body != null ? playback.Body.position : (Vector2)playback.transform.position;
        _waypoints.Add(new Sample { position = origin, eta = 0f });

        AppendFutureCheckpoints(playback, progression, lookaheadCheckpoints);

        BuildSamples(Mathf.Max(0.25f, sampleSpacing));
    }

    private void AppendFutureCheckpoints(NpcCommandPlayback playback, NpcProgressionController progression, int lookaheadCheckpoints)
    {
        if (progression == null || !progression.IsRunning || progression.IsChainComplete || lookaheadCheckpoints <= 0)
        {
            return;
        }

        IReadOnlyList<ProgressCheckpoint> chain = progression.CheckpointChain;
        int current = progression.CurrentCheckpointIndex;
        if (chain == null || current < 0 || current >= chain.Count - 1)
        {
            return;
        }

        // Time left in the segment currently being replayed. Once playback has run out
        // of commands (arrival grace / recovery) this is 0, which simply means "the NPC
        // is at its next checkpoint about now" - close enough for weighting.
        float eta = playback.RemainingTickCount * Time.fixedDeltaTime;

        for (int step = 0; step < lookaheadCheckpoints; step++)
        {
            int targetIndex = current + 1 + step;
            if (targetIndex >= chain.Count || chain[targetIndex] == null)
            {
                break;
            }

            _waypoints.Add(new Sample { position = chain[targetIndex].Anchor, eta = eta });
            HasRoute = true;

            // Cost of the segment that starts at the checkpoint we just added.
            float segmentSeconds = progression.EstimateSegmentSeconds(targetIndex);
            if (segmentSeconds <= 0f)
            {
                break;
            }

            eta += segmentSeconds;
        }
    }

    private void BuildSamples(float spacing)
    {
        if (_waypoints.Count == 0)
        {
            return;
        }

        _samples.Add(_waypoints[0]);

        for (int i = 1; i < _waypoints.Count && _samples.Count < MaxSamples; i++)
        {
            Sample from = _waypoints[i - 1];
            Sample to = _waypoints[i];

            float length = Vector2.Distance(from.position, to.position);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / spacing));

            for (int s = 1; s <= steps && _samples.Count < MaxSamples; s++)
            {
                float t = (float)s / steps;
                _samples.Add(new Sample
                {
                    position = Vector2.Lerp(from.position, to.position, t),
                    eta = Mathf.Lerp(from.eta, to.eta, t)
                });
            }
        }
    }

    /// <summary>
    /// Distance from a world point to the closest point on the forecast polyline, and
    /// the ETA carried by that closest sample. Returns false when there is no forecast.
    /// </summary>
    public bool TryGetClosest(Vector2 worldPosition, out float distance, out float eta)
    {
        distance = 0f;
        eta = 0f;

        if (_samples.Count == 0)
        {
            return false;
        }

        float bestSqr = float.MaxValue;
        float bestEta = 0f;

        for (int i = 0; i < _samples.Count; i++)
        {
            float sqr = (_samples[i].position - worldPosition).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestEta = _samples[i].eta;
            }
        }

        distance = Mathf.Sqrt(bestSqr);
        eta = bestEta;
        return true;
    }
}
