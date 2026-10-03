namespace Lunaria.Game.Tasks;

public sealed record TaskStepState(ulong StepId, IReadOnlyDictionary<ulong, TaskActionState> Actions);

public sealed record TaskActionState(uint Progress, uint MaxProgress)
{
    public bool IsComplete => MaxProgress > 0 && Progress >= MaxProgress;
}
