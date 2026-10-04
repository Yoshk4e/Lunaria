using Msg;

namespace Lunaria.Game.Player.Gameplay;

public sealed record WalletChanged : IGameplayEvent;

public sealed record BagChanged(EnmItemReason Reason) : IGameplayEvent;

public sealed record CooldownStarted(uint CdType, uint ReadyUnix) : IGameplayEvent;

public sealed record StaminaChanged(int Stamina) : IGameplayEvent;

public sealed record SatietyChanged(int Satiety) : IGameplayEvent;

public sealed record VitalsChanged(IReadOnlyList<ulong> InstIds) : IGameplayEvent;

public sealed record LiquidChanged : IGameplayEvent;

public sealed record SkillGroupsChanged(ulong InstId, IReadOnlyList<uint> Groups) : IGameplayEvent;

public sealed record BuffsChanged(IReadOnlyList<(PBBuffData Data, bool Refreshed)> Updated, IReadOnlyList<uint> Removed) : IGameplayEvent;
