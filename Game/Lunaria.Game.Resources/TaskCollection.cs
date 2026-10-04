namespace Lunaria.Game.Resources;

/// <summary>
/// A collectable a quest places while one of its steps runs (task persistent row with SystemType 4). The client
/// only shows it when the server sends it with from_type ECOLLECT_FROM_TASK, its location and its rotation.
/// </summary>
public sealed record TaskCollection(
    ulong Id,
    uint TaskType,
    uint TaskId,
    ulong StepStart,
    ulong StepEnd,
    ulong MapId,
    uint TemplateId,
    int X,
    int Y,
    int Z,
    int Pitch,
    int Yaw,
    int Roll
)
{
    public bool CoversStep(ulong stepId) => stepId >= StepStart && stepId <= StepEnd;
}
