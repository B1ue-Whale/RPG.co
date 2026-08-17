using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A saved player run from one ProgressCheckpoint to another, as a MotorCommand
/// stream. Checkpoints are referenced by id (not scene object reference) so this asset
/// stays valid independent of which scene is currently loaded.
/// </summary>
[CreateAssetMenu(fileName = "NpcRecording", menuName = "NPC AI/Npc Recording")]
public class NpcRecording : ScriptableObject
{
    [SerializeField] private string startCheckpointId;
    [SerializeField] private string endCheckpointId;
    [SerializeField] private MotorCommand[] commands = System.Array.Empty<MotorCommand>();

    public string StartCheckpointId => startCheckpointId;
    public string EndCheckpointId => endCheckpointId;
    public IReadOnlyList<MotorCommand> Commands => commands;

    public void Initialize(string startCheckpointId, string endCheckpointId, IReadOnlyList<MotorCommand> commands)
    {
        this.startCheckpointId = startCheckpointId;
        this.endCheckpointId = endCheckpointId;

        this.commands = new MotorCommand[commands.Count];
        for (int i = 0; i < commands.Count; i++)
        {
            this.commands[i] = commands[i];
        }
    }
}
