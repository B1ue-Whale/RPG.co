using UnityEngine;

/// <summary>Kind of target an NpcVisionSensor can report.</summary>
public enum VisionTargetKind
{
    None,
    Player,
    Bug
}

/// <summary>
/// Result of a single NpcVisionSensor.Sense() call: what was seen (if anything) and
/// where it was, in world space.
/// </summary>
public readonly struct VisionDetection
{
    public readonly VisionTargetKind Kind;
    public readonly Vector3 Position;

    public VisionDetection(VisionTargetKind kind, Vector3 position)
    {
        Kind = kind;
        Position = position;
    }

    public static VisionDetection None => new VisionDetection(VisionTargetKind.None, default);
}
