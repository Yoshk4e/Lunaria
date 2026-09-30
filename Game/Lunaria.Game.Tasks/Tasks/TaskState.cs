namespace Lunaria.Game.Tasks;

public sealed record TaskState(uint Type, uint TaskId, TaskStepState CurrentStep);
