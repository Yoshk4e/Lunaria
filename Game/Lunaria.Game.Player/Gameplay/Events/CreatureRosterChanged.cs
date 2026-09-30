namespace Lunaria.Game.Player.Gameplay;

public sealed record CreatureRosterChanged(IReadOnlyList<Msg.CmdSilverCreatureItem> Added) : IGameplayEvent;
