using Lunaria.Game.Tasks;

namespace Lunaria.Game.Player.Gameplay;

public readonly record struct TaskProgressed(TaskProgressResult Progress) : IGameplayEvent;
