using Google.Protobuf;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player;

/// <summary>Reward hooks have already run. Do not apply these rewards again.</summary>
public sealed record RewardDelivery
{
    public EnmItemReason Reason { get; init; }
    public IReadOnlyList<ItemGrant> Credited { get; init; } = [];
    public IReadOnlyList<ItemGrant> Stored { get; init; } = [];
    /// <summary>Unclaimed attachments retained on the source mail.</summary>
    public IReadOnlyList<ItemGrant> Undelivered { get; init; } = [];
    /// <summary>Direct delivery failures, including items subsequently mailed or deferred.</summary>
    public IReadOnlyList<ItemGrant> DirectFailures { get; init; } = [];
    public IReadOnlyList<ItemGrant> Mailed { get; init; } = [];
    /// <summary>Overflow retained in the role save until mailbox space is available.</summary>
    public IReadOnlyList<ItemGrant> Deferred { get; init; } = [];
    public IReadOnlyList<CmdSilverCreatureItem> CollectedCreatures { get; init; } = [];
    public IReadOnlyList<ItemGrant> CreatureFailures { get; init; } = [];
    public IReadOnlyList<uint> UnlockedGuides { get; init; } = [];
    public ulong TeamExpFromItems { get; init; }
    public ulong TeamExpFromReason { get; init; }
    public ulong TeamExpAwarded => checked(TeamExpFromItems + TeamExpFromReason);
    public IReadOnlyList<CharacterData> Newcomers { get; init; } = [];
    public IReadOnlyList<CSMotiveElem> NewMotives { get; init; } = [];
    public IReadOnlyList<uint> ChangedBattlePasses { get; init; } = [];
    public int? Stamina { get; init; }

    /// <summary>Presentation only. Hooks already queued the state updates.</summary>
    public IReadOnlyList<IMessage> Presentation { get; init; } = [];

    public static RewardDelivery Empty { get; } = new();

    public bool HasChanges => Credited.Count + Stored.Count + Undelivered.Count + Mailed.Count + Deferred.Count
                              + CollectedCreatures.Count + CreatureFailures.Count + UnlockedGuides.Count
                              + Newcomers.Count + NewMotives.Count + ChangedBattlePasses.Count > 0
                              || TeamExpAwarded > 0 || Stamina is not null;

    public static RewardDelivery Combine(IEnumerable<RewardDelivery> deliveries)
    {
        var parts = deliveries.ToArray();
        var reason = parts.FirstOrDefault()?.Reason ?? default;

        if (parts.Any(part => part.Reason != reason))
            throw new ArgumentException("Combined rewards must have the same reason.", nameof(deliveries));

        return new RewardDelivery {
            Reason = reason,
            Credited = parts.SelectMany(p => p.Credited).ToArray(),
            Stored = parts.SelectMany(p => p.Stored).ToArray(),
            Undelivered = parts.SelectMany(p => p.Undelivered).ToArray(),
            DirectFailures = parts.SelectMany(p => p.DirectFailures).ToArray(),
            Mailed = parts.SelectMany(p => p.Mailed).ToArray(),
            Deferred = parts.SelectMany(p => p.Deferred).ToArray(),
            CollectedCreatures = parts.SelectMany(p => p.CollectedCreatures).ToArray(),
            CreatureFailures = parts.SelectMany(p => p.CreatureFailures).ToArray(),
            UnlockedGuides = parts.SelectMany(p => p.UnlockedGuides).Distinct().ToArray(),
            Newcomers = parts.SelectMany(p => p.Newcomers).ToArray(),
            NewMotives = parts.SelectMany(p => p.NewMotives).ToArray(),
            ChangedBattlePasses = parts.SelectMany(p => p.ChangedBattlePasses).Distinct().ToArray(),
            TeamExpFromItems = parts.Aggregate(seed: 0UL, (sum, p) => checked(sum + p.TeamExpFromItems)),
            TeamExpFromReason = parts.Aggregate(seed: 0UL, (sum, p) => checked(sum + p.TeamExpFromReason)),
            Stamina = parts.LastOrDefault(p => p.Stamina is not null)?.Stamina,
            Presentation = parts.SelectMany(p => p.Presentation).ToArray()
        };
    }
}
