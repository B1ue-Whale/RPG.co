/// <summary>
/// One physics tick's worth of logical commands for <see cref="CharacterMotor2D"/>
/// and <see cref="InteractionAgent"/>. Fields mirror exactly what
/// CharacterMotor2D.SetMoveInput / RequestJump / SetJumpHeld and
/// InteractionAgent.TryInteract were called with during that tick - nothing derived
/// from resulting position/velocity/state.
/// </summary>
[System.Serializable]
public struct MotorCommand
{
    public float moveInput;
    public bool jumpRequested;
    public bool jumpHeld;
    public bool interactRequested;
}
