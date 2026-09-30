using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player.Gameplay;

public sealed record WantedResourcesChanged(CmdWantedResource Before, EWantedAwardType? AwardType = null) : IGameplayEvent;
