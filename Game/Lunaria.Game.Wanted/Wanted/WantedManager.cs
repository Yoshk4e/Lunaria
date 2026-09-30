using Lunaria.Game.Resources;
using Lunaria.Game.Resources.Tables;
using Msg;

namespace Lunaria.Game.Wanted;

public sealed record WantedBionics(uint EntryId, uint UniqId);

public sealed record WantedStepAward(uint AwardId, uint Type, IReadOnlyList<uint> Options, bool Chosen);

public sealed record WantedStepState(
    uint Step,
    EnmWantedStepStatus Status,
    uint ProcessId,
    uint EventId,
    bool EventDone,
    IReadOnlyList<WantedStepAward> Awards
);

public sealed record WantedAdventure(uint AdventureId, uint ContentId, uint DialogId, int OptionResult);

public sealed record WantedStepSnapshot(
    EnmWantedStepStatus Status,
    uint ProcessId,
    uint EventId,
    bool EventDone,
    IReadOnlyList<WantedStepAward> Awards
);

public sealed record WantedRunSnapshot(
    uint EntryId,
    uint RouteId,
    uint MaxStep,
    uint Step,
    WantedStepSnapshot Current,
    IReadOnlyList<(uint ProcessId, uint EventId)> History,
    IReadOnlyList<uint> Blesses,
    IReadOnlyList<WantedBionics> Bionics,
    IReadOnlyList<uint> Relics,
    uint CoinsTotal,
    uint ReviveCount,
    (uint Id, int X, int Y, int Z)? ResetPoint,
    IReadOnlyList<WantedAdventure> Adventures,
    IReadOnlyDictionary<uint, uint> ShopBuys,
    uint LastBionicsId = 0,
    IReadOnlyList<uint>? RedeemedSteps = null
);

public sealed partial class WantedManager(GameData assets, Random? random = null)
{
    private readonly SortedDictionary<uint, uint> _finishes = [];
    private readonly Random _random = random ?? new();

    public Func<uint, bool> IsConditionMet { get; set; } = id => id == 0;

    private RunState? _run;

    public bool IsDirty { get; private set; }

    public IReadOnlyDictionary<uint, uint> Finishes => _finishes;

    public bool IsRunning => _run is not null;

    public uint CurrentEntryId => _run?.EntryId ?? 0;

    public uint CurrentStep => _run?.Step ?? 0;

    public uint CurrentEventId => _run?.Current.EventId ?? 0;

    public uint ReviveCount => _run?.ReviveCount ?? 0;

    public IReadOnlyList<WantedAdventure> Adventures => _run?.Adventures ?? [];

    public void Load(IEnumerable<(uint EntryId, uint Count)> finishes, WantedRunSnapshot? run = null)
    {
        _finishes.Clear();

        foreach (var (entryId, count) in finishes)
        {
            if (assets.Wanted.Entry(entryId) is not null)
                _finishes[entryId] = count;
        }
        _run = null;

        if (run is not null)
            AdoptRun(run);
        IsDirty = false;
    }

    public WantedRunSnapshot? CaptureRun() =>
        _run is not {} run ?
            null :
            new WantedRunSnapshot(
                run.EntryId,
                run.RouteId,
                run.MaxStep,
                run.Step,
                new WantedStepSnapshot(
                    run.Current.Status, run.Current.ProcessId, run.Current.EventId,
                    run.Current.EventDone, run.Current.Awards),
                run.History.ToList(),
                run.Blesses.ToList(),
                run.Bionics.ToList(),
                run.Relics.ToList(),
                run.CoinsTotal,
                run.ReviveCount,
                run.ResetPoint,
                run.Adventures.ToList(),
                new SortedDictionary<uint, uint>(run.ShopBuys), run.LastBionicsId, run.RedeemedSteps.Order().ToArray());

    private void AdoptRun(WantedRunSnapshot snapshot)
    {
        if (assets.Wanted.Entry(snapshot.EntryId) is not {} entry || assets.Wanted.MaxStep(snapshot.RouteId) == 0)
            return;

        var maxStep = assets.Wanted.MaxStep(snapshot.RouteId);
        var step = Math.Clamp(snapshot.Step, min: 1, maxStep);
        var process = assets.Wanted.StepsOf(snapshot.RouteId, step).FirstOrDefault(p => p.Id == snapshot.Current.ProcessId);
        var currentEvent = assets.Wanted.Event(snapshot.Current.EventId);

        var current = step == snapshot.Step && process is not null && currentEvent is not null
                      && assets.Policy.Wanted.EventPools[process.Pool].Contains(currentEvent.Id) ?
            new WantedStepState(step, snapshot.Current.Status, snapshot.Current.ProcessId, snapshot.Current.EventId,
                snapshot.Current.EventDone, snapshot.Current.Awards) :
            BuildStep(entry, snapshot.RouteId, step);

        var blesses = snapshot.Blesses.Where(id => assets.Wanted.Bless(id) is not null).ToList();
        var relics = snapshot.Relics.Where(id => assets.Wanted.Relic(id) is not null).ToList();

        var bionics = snapshot.Bionics
            .Where(bionics => assets.Wanted.Creature(bionics.EntryId) is not null)
            .Take((int)assets.Wanted.CreatureMaxCount)
            .ToList();

        _run = new RunState(
            snapshot.EntryId,
            snapshot.RouteId,
            maxStep,
            step,
            current,
            snapshot.History.ToList(),
            blesses,
            bionics,
            relics,
            snapshot.CoinsTotal,
            snapshot.ReviveCount,
            snapshot.ResetPoint,
            snapshot.Adventures.ToList(),
            new SortedDictionary<uint, uint>(snapshot.ShopBuys.ToDictionary(pair => pair.Key, pair => pair.Value)));
        _run.LastBionicsId = Math.Max(_run.LastBionicsId, snapshot.LastBionicsId);
        _run.RedeemedSteps.UnionWith(snapshot.RedeemedSteps ?? []);
    }

    public void ClearDirty() => IsDirty = false;

    public bool IsEventComplete(uint eventId) => _run is {} run
                                                 && (run.Current.EventId == eventId ?
                                                     run.Current.EventDone :
                                                     run.History.Any(e => e.EventId == eventId));

    public SCWantedOutsideData ToOutsideData()
    {
        var reply = new SCWantedOutsideData { Result = 0 };

        foreach (var posterId in assets.Wanted.AllPosters)
        {
            var typeData = new CmdWantedTypeData { TypeId = posterId };

            foreach (var entry in assets.Wanted.EntriesOf(posterId))
            {
                typeData.WantedList.Add(new CmdOneWantedBrief {
                    Id = entry.Id,
                    FinishCount = _finishes.GetValueOrDefault(entry.Id)
                });
            }
            reply.WantedTypes.Add(typeData);
        }

        reply.CurrentId = _run?.EntryId ?? 0;
        reply.CurrentStep = _run?.Step ?? 0;
        return reply;
    }

    public int CheckEnter(uint entryId)
    {
        if (assets.Wanted.Entry(entryId) is not {} entry)
            return (int)EnmTextCode.EnmTextWantedNotInWanted;

        if (_run is not null)
            return (int)EnmTextCode.EnmTextWantedIsInWanted;

        if (entry.Difficulty > 1)
        {
            var previous = PreviousDifficultyEntry(entry);

            if (previous is null || _finishes.GetValueOrDefault(previous.Id) == 0)
                return (int)EnmTextCode.EnmTextWantedAdventureNotExists;
        }

        return 0;
    }

    public void Enter(uint entryId)
    {
        var entry = assets.Wanted.Entry(entryId)!;
        var route = _finishes.GetValueOrDefault(entryId) == 0 ? entry.FristRouteId : entry.NormalRouteId;
        var maxStep = assets.Wanted.MaxStep(route);

        _run = new RunState(
            entryId, route, maxStep, step: 1, BuildStep(entry, route, step: 1), [],
            [], [], [], coinsTotal: 0, reviveCount: 0, resetPoint: null, [], []);
        Dirty();
    }

    public SCWantedInsideData? ToInsideData()
    {
        if (_run is not {} run)
            return null;

        var reply = new SCWantedInsideData {
            Result = 0,
            StepData = ToStepData(run.Current),
            ResetPoint = run.ResetPoint?.Id ?? 0,
            WantedId = run.EntryId,
            MaxStep = run.MaxStep,
            ReviveCount = run.ReviveCount
        };

        reply.EventHistory.AddRange(run.History
            .Select(pair => new CmdWantedEventInfo { ProcessId = pair.ProcessId, EventId = pair.EventId }));
        return reply;
    }

    public SCWantedStepNtf? ToStepNotification() =>
        _run is {} run ? new SCWantedStepNtf { StepData = ToStepData(run.Current) } : null;

    public (bool Completed, SCWantedStepNtf? Notification, IReadOnlyList<ItemGrant> StepDrop) OnBattleEnded(bool success)
    {
        if (_run is not {} run || !success || run.Current.EventDone)
            return (false, null, []);

        var @event = assets.Wanted.Event(run.Current.EventId);

        if (@event is null || !IsBattleType(@event.WantedEventType))
            return (false, null, []);

        return CompleteEvent(run);
    }

    public (bool Completed, SCWantedStepNtf? Notification, IReadOnlyList<ItemGrant> StepDrop) OnAdventureResolved(
        uint adventureId,
        uint contentId,
        uint dialogId,
        int optionResult
    )
    {
        if (_run is not {} run || run.Current.EventDone)
            return (false, null, []);

        var @event = assets.Wanted.Event(run.Current.EventId);

        if (@event is null || @event.WantedEventType != (uint)WantedEventType.Adventure)
            return (false, null, []);

        if (assets.Wanted.Adventure(adventureId) is not {} adventure
            || !assets.Wanted.TryAdvanceAdventure(adventureId, contentId, dialogId, out var next))
            return (false, null, []);

        if (assets.Wanted.Npc(run.Current.EventId) is { NpcType: (uint)WantedNpcType.Adventure } npc && npc.Params != adventureId)
            return (false, null, []);

        var previous = run.Adventures.LastOrDefault(a => a.AdventureId == adventureId);
        var expected = adventure.ContentHeadId;

        if (previous is not null)
        {
            if (previous.ContentId == contentId && previous.DialogId == dialogId) return (false, null, []);

            if (!assets.Wanted.TryAdvanceAdventure(adventureId, previous.ContentId, previous.DialogId, out expected))
                return (false, null, []);
        }
        if (contentId != expected) return (false, null, []);

        run.Adventures.RemoveAll(a => a.AdventureId == adventureId);
        run.Adventures.Add(new WantedAdventure(adventureId, contentId, dialogId, optionResult));
        Dirty();
        return next == 0 ? CompleteEvent(run) : (false, null, []);
    }

    public (int Result, bool Finished, SCWantedStepNtf? Notification) ChooseAward(
        uint stepAwardId,
        uint award,
        ulong replacedBionicsUniqId
    )
    {
        if (_run is not {} run)
            return ((int)EnmTextCode.EnmTextWantedNotInWanted, false, null);

        var offered = run.Current.Awards.FirstOrDefault(a => a.AwardId == stepAwardId);

        if (offered is null || offered.Chosen || !offered.Options.Contains(award))
            return ((int)EnmTextCode.EnmTextWantedStepWrongAward, false, null);

        var code = ApplyAward(run, offered.Type, award, replacedBionicsUniqId);

        if (code != 0)
            return (code, false, null);

        var awards = run.Current.Awards.Select(a => a.AwardId == stepAwardId ? a with { Chosen = true } : a).ToList();
        var finished = awards.All(a => a.Chosen);

        run.Current = run.Current with {
            Awards = awards,
            Status = finished ? EnmWantedStepStatus.EnmWssAwardFinished : EnmWantedStepStatus.EnmWssTaskFinished
        };

        if (finished && run.Step < run.MaxStep)
            AdvanceStep(run);

        Dirty();
        return (0, finished, ToStepNotification());
    }

    public CmdWantedOneShop? ToShop(uint shopId)
    {
        if (_run is not {} run || assets.Wanted.Shop(shopId) is not {} shop)
            return null;

        var reply = new CmdWantedOneShop { ShopId = shopId };

        foreach (var good in OfferedGoods(shop))
        {
            reply.GoodsList.Add(new CmdWantedGoods {
                GoodsId = good,
                BuyCount = run.ShopBuys.GetValueOrDefault(good),
                Price = GoodPrice(shop.Type, good)
            });
        }

        return reply;
    }

    /// <summary>Creature purchases need a free slot because the shop request cannot choose a replacement.</summary>
    public (int Result, uint SandCost) CheckShopBuy(uint shopId, uint goodsId, uint buyCount)
    {
        if (_run is null)
            return ((int)EnmTextCode.EnmTextWantedNotInWanted, 0);

        if (buyCount == 0)
            return ((int)EnmTextCode.EnmTextWantedShopGoodsCountNotEnough, 0);

        var shop = assets.Wanted.Shop(shopId);

        if (shop is null || !OfferedGoods(shop).Contains(goodsId))
            return ((int)EnmTextCode.EnmTextWantedShopGoodsNotExists, 0);

        var limit = GoodLimit(shop.Type, goodsId);
        var bought = _run?.ShopBuys.GetValueOrDefault(goodsId) ?? 0;

        if ((ulong)bought + buyCount > uint.MaxValue || limit > 0 && (ulong)bought + buyCount > limit)
            return ((int)EnmTextCode.EnmTextWantedShopGoodsCountNotEnough, 0);

        if (shop.Type == (uint)WantedPosterRandomRewardType.Creature && _run is {} run && run.Bionics.Count + buyCount > assets.Wanted.CreatureMaxCount)
            return ((int)EnmTextCode.EnmTextWantedShopGoodsCountNotEnough, 0);

        var cost = (ulong)GoodPrice(shop.Type, goodsId) * buyCount;

        return cost > uint.MaxValue ?
            ((int)EnmTextCode.EnmTextWantedShopGoodsCountNotEnough, 0) :
            (0, (uint)cost);
    }

    public (CmdWantedGoods Goods, IReadOnlyList<uint> Blesses, IReadOnlyList<uint> Relics, IReadOnlyList<uint> CreatureEntries)
        CommitShopBuy(uint shopId, uint goodsId, uint buyCount)
    {
        var shop = assets.Wanted.Shop(shopId)!;
        var blesses = new List<uint>();
        var relics = new List<uint>();
        var creatures = new List<uint>();

        for (uint i = 0; i < buyCount; i++)
        {
            switch (shop.Type)
            {
                case (uint)WantedPosterRandomRewardType.Bless when assets.Wanted.BlessGood(goodsId) is {} good:
                    blesses.Add(good.BlessId);
                    break;
                case (uint)WantedPosterRandomRewardType.Relic when assets.Wanted.RelicGood(goodsId) is {} good:
                    relics.Add(good.RelicId);
                    break;
                case (uint)WantedPosterRandomRewardType.Creature when assets.Wanted.CreatureGood(goodsId) is {} good:
                    creatures.Add(good.CreatureEntryId);
                    break;
            }
        }

        var bought = _run!.ShopBuys.GetValueOrDefault(goodsId);
        _run.ShopBuys[goodsId] = bought + buyCount;

        foreach (var bless in blesses)
        {
            _run.Blesses.Add(bless);
        }
        _run.Relics.AddRange(relics);

        foreach (var entry in creatures)
        {
            AddBionics(entry);
        }
        Dirty();

        return (
            new CmdWantedGoods {
                GoodsId = goodsId,
                BuyCount = bought + buyCount,
                Price = GoodPrice(shop.Type, goodsId)
            },
            blesses,
            relics,
            creatures);
    }

    public uint? NextReviveCost() =>
        _run is {} run ? assets.Wanted.ReviveCost((int)run.ReviveCount + 1) : null;

    public int CommitRevive()
    {
        if (_run is not {} run)
            return (int)EnmTextCode.EnmTextWantedNotInWanted;

        if (assets.Wanted.ReviveCost((int)run.ReviveCount + 1) is null)
            return (int)EnmTextCode.EnmTextWantedReviveAllFailed;

        run.ReviveCount++;
        Dirty();
        return 0;
    }

    public bool CanRecover() => _run is not null;

    public void SetResetPoint(uint id, (int X, int Y, int Z) position)
    {
        if (_run is null)
            return;

        _run.ResetPoint = (id, position.X, position.Y, position.Z);
        Dirty();
    }

    public CmdWantedResource ToResource()
    {
        var resource = new CmdWantedResource();

        if (_run is not {} run)
            return resource;

        resource.BlessIds.AddRange(run.Blesses);
        resource.CoinsCurrent = 0;
        resource.CoinsTotal = run.CoinsTotal;
        resource.RelicsIds.AddRange(run.Relics);

        resource.Bionics.AddRange(run.Bionics.Select(bionics => new CmdBionics {
            BionicsId = bionics.EntryId,
            UniqId = bionics.UniqId
        }));

        var bonds = new SortedDictionary<uint, uint>();

        foreach (var blessId in run.Blesses)
        {
            var bless = assets.Wanted.Bless(blessId);

            if (bless is null)
                continue;

            foreach (var requirement in bless.Bond)
            {
                var parts = requirement.Split(separator: '=', count: 2);

                if (parts.Length == 2
                    && uint.TryParse(parts[0], out var bondId)
                    && uint.TryParse(parts[1], out var points))
                    bonds[bondId] = bonds.GetValueOrDefault(bondId) + points;
            }
        }

        foreach (var (bondId, point) in bonds)
        {
            resource.Bonds.Add(new CmdBond { Id = bondId, Point = point });
        }

        return resource;
    }

    /// <summary>
    /// An empty request means all owned bionics. Send their IDs and let the client fill attributes from its tables.
    /// </summary>
    public IReadOnlyList<PBCharacterAttribData> BionicsAttribData(IReadOnlyList<ulong> requested)
    {
        if (_run is not {} run)
            return [];

        return run.Bionics
            .Where(bionics => requested.Count == 0 || requested.Contains(bionics.UniqId))
            .Select(bionics => new PBCharacterAttribData { InstId = bionics.UniqId })
            .ToList();
    }

    public int GiveUpBionics(ulong uniqId)
    {
        if (_run is not {} run)
            return (int)EnmTextCode.EnmTextWantedNotInWanted;

        var bionics = run.Bionics.FirstOrDefault(b => b.UniqId == uniqId);

        if (bionics is null)
            return (int)EnmTextCode.EnmTextWantedBionicsReplaceIdNotExist;

        run.Bionics.Remove(bionics);
        Dirty();
        return 0;
    }

    public (int Result, SCWantedOver Settlement, IReadOnlyList<ItemGrant> Grants) Over()
    {
        if (_run is not {} run)
            return ((int)EnmTextCode.EnmTextWantedNotInWanted, new SCWantedOver(), []);

        var entry = assets.Wanted.Entry(run.EntryId)!;

        var victory = run.Current.Status == EnmWantedStepStatus.EnmWssAwardFinished
                      && run.Step >= run.MaxStep;
        var first = victory && _finishes.GetValueOrDefault(run.EntryId) == 0;

        var grants = first && entry.FirstAward != 0 ? assets.DropTable.Roll(entry.FirstAward, _random) : [];

        if (victory)
        {
            _finishes[run.EntryId] = _finishes.GetValueOrDefault(run.EntryId) + 1;
        }

        var settlement = new SCWantedOver {
            Result = 0,
            WantedId = run.EntryId,
            FinishStep = run.Step,
            Victory = victory,
            FinishCount = _finishes.GetValueOrDefault(run.EntryId),
            Resource = ToResource(),
            MaxStep = run.MaxStep,
            BattlePassAddScore = victory ? entry.ExpAwardDisplay : 0,
            BattlePassId = 1001
        };
        settlement.AwardFirst.AddRange(first ? GrantsToCmdItems(grants) : []);

        _run = null;
        Dirty();
        return (0, settlement, grants);
    }

    public int Leave()
    {
        if (_run is null)
            return (int)EnmTextCode.EnmTextWantedNotInWanted;

        _run = null;
        Dirty();
        return 0;
    }

    private WantedStepState BuildStep(PWantedPosterEntryTable entry, uint route, uint step)
    {
        var processes = assets.Wanted.StepsOf(route, step);
        var process = processes.Count == 0 ? null : processes[_random.Next(processes.Count)];
        var processId = process?.Id ?? 0;
        var eventId = PickEvent(process);

        return new WantedStepState(
            step,
            eventId == 0 ? EnmWantedStepStatus.EnmWssSelectEvent : EnmWantedStepStatus.EnmWssStart,
            processId,
            eventId,
            EventDone: false,
            []);
    }

    /// <summary>Event pools use inferred server rules because the recovered tables omit membership.</summary>
    private uint PickEvent(PWantedPosterProcessTable? process)
    {
        if (process is null)
            return 0;

        var candidates = assets.Policy.Wanted.EventPools[process.Pool];
        return candidates[_random.Next(candidates.Length)];
    }

    private (bool, SCWantedStepNtf?, IReadOnlyList<ItemGrant>) CompleteEvent(RunState run)
    {
        var drop = StepDrop(run).ToList();
        var awards = BuildAwards(run, drop);
        var sand = assets.Items.CurrencyItemFor((int)MoneyType.ThoughtSand);
        run.CoinsTotal = (uint)Math.Min(uint.MaxValue, (ulong)run.CoinsTotal + (ulong)drop.Where(g => g.ItemId == sand).Sum(g => (long)g.Count));

        run.Current = run.Current with {
            EventDone = true,
            Status = EnmWantedStepStatus.EnmWssTaskFinished,
            Awards = awards
        };

        if (run.Current.Awards.Count == 0)
        {
            if (run.Step < run.MaxStep)
                AdvanceStep(run);
            else
                run.Current = run.Current with { Status = EnmWantedStepStatus.EnmWssAwardFinished };
        }

        Dirty();
        return (true, ToStepNotification(), drop);
    }

    public (bool Completed, SCWantedStepNtf? Notification, IReadOnlyList<ItemGrant> StepDrop) OnTaskStepsPassed(IReadOnlyList<ulong> steps)
    {
        if (_run is not {} run || run.Current.EventDone
                               || assets.Wanted.Event(run.Current.EventId) is not { WantedEventType: (uint)WantedEventType.Shop } row
                               || !steps.Contains(row.TaskStepOfEventFinish)) return (false, null, []);

        return CompleteEvent(run);
    }

    private IReadOnlyList<ItemGrant> StepDrop(RunState run)
    {
        var row = assets.Wanted.StepCount(run.EntryId, run.Step);
        return row is null || row.Award == 0 ? [] : assets.DropTable.Roll(row.Award, _random);
    }

    private int ApplyAward(RunState run, uint type, uint award, ulong replacedBionicsUniqId)
    {
        switch (type)
        {
            case (uint)EWantedAwardType.AddBlessSelect:
                if (assets.Wanted.Bless(award) is null)
                    return (int)EnmTextCode.EnmTextWantedStepWrongAward;

                run.Blesses.Add(award);
                return 0;

            case (uint)EWantedAwardType.AddRelicSelect:
                if (assets.Wanted.Relic(award) is null)
                    return (int)EnmTextCode.EnmTextWantedStepWrongAward;

                run.Relics.Add(award);
                return 0;

            case (uint)EWantedAwardType.AddCreatureSelect:
                if (assets.Wanted.Creature(award) is null)
                    return (int)EnmTextCode.EnmTextWantedStepWrongAward;

                return AddBionics(award, replacedBionicsUniqId);

            default:
                return (int)EnmTextCode.EnmTextWantedStepWrongAward;
        }
    }

    private int AddBionics(uint entryId, ulong replacedUniqId = 0)
    {
        var run = _run!;
        var nextId = checked(run.LastBionicsId + 1);

        if (run.Bionics.Count >= assets.Wanted.CreatureMaxCount)
        {
            var replaced = run.Bionics.FirstOrDefault(b => b.UniqId == replacedUniqId);

            if (replaced is null)
                return (int)EnmTextCode.EnmTextWantedBionicsReplaceIdNotExist;

            run.Bionics.Remove(replaced);
        }

        run.LastBionicsId = nextId;
        run.Bionics.Add(new WantedBionics(entryId, nextId));
        return 0;
    }

    private void AdvanceStep(RunState run)
    {
        var entry = assets.Wanted.Entry(run.EntryId)!;
        var next = run.Step + 1;
        run.History.Add((run.Current.ProcessId, run.Current.EventId));
        run.Current = BuildStep(entry, run.RouteId, next);

        if (assets.Wanted.Npc(run.Current.EventId) is { NpcType: (uint)WantedNpcType.Adventure } npc)
            run.Adventures.RemoveAll(a => a.AdventureId == npc.Params);
        run.Step = next;
    }

    private PWantedPosterEntryTable? PreviousDifficultyEntry(PWantedPosterEntryTable entry) =>
        assets.Wanted.EntriesOf(entry.WantedPosterId)
            .FirstOrDefault(other => other.Difficulty == entry.Difficulty - 1);

    private static bool IsBattleType(uint wantedEventType) =>
        wantedEventType is (uint)WantedEventType.Battle
            or (uint)WantedEventType.Elite
            or (uint)WantedEventType.Boss
            or (uint)WantedEventType.Battle2
            or (uint)WantedEventType.EndlessBattle;

    private IEnumerable<uint> OfferedGoods(PWantedPosterShop shop)
    {
        foreach (var (group, count) in ParseShopParams(shop))
        foreach (var goodsId in assets.Wanted.ShopGoods(shop.Type, group).Take((int)count))
        {
            yield return goodsId;
        }
    }

    private static IEnumerable<(uint Group, uint Count)> ParseShopParams(PWantedPosterShop shop)
    {
        foreach (var param in shop.Param)
        {
            var parts = param.Split(separator: '=', count: 2);

            if (parts.Length == 2
                && uint.TryParse(parts[0], out var group)
                && uint.TryParse(parts[1], out var count))
                yield return (group, count);
        }
    }

    private uint GoodPrice(uint kind, uint goodsId) => kind switch {
        (uint)WantedPosterRandomRewardType.Bless => assets.Wanted.BlessGood(goodsId)?.CostNum ?? 0,
        (uint)WantedPosterRandomRewardType.Relic => assets.Wanted.RelicGood(goodsId)?.CostNum ?? 0,
        (uint)WantedPosterRandomRewardType.Creature => assets.Wanted.CreatureGood(goodsId)?.CostNum ?? 0,
        _ => 0
    };

    private uint GoodLimit(uint kind, uint goodsId) => kind switch {
        (uint)WantedPosterRandomRewardType.Bless => assets.Wanted.BlessGood(goodsId)?.LimitNum ?? 0,
        (uint)WantedPosterRandomRewardType.Relic => assets.Wanted.RelicGood(goodsId)?.LimitNum ?? 0,
        (uint)WantedPosterRandomRewardType.Creature => assets.Wanted.CreatureGood(goodsId)?.LimitNum ?? 0,
        _ => 0
    };

    private static IEnumerable<CmdItem> GrantsToCmdItems(IReadOnlyList<ItemGrant> grants) =>
        grants.Select(grant => new CmdItem { ItemId = grant.ItemId, ItemNum = grant.Count });

    private static CmdWantedStep ToStepData(WantedStepState step)
    {
        var data = new CmdWantedStep {
            Step = step.Step,
            Status = step.Status
        };

        if (step.EventId != 0)
            data.EventInfo.Add(new CmdWantedEventInfo { ProcessId = step.ProcessId, EventId = step.EventId });

        foreach (var award in step.Awards)
        {
            var cmd = new CmdWantedStepAward {
                Id = award.AwardId,
                Type = award.Type,
                Finish = award.Chosen
            };
            cmd.AwardId.AddRange(award.Options.Select(option => (ulong)option));
            data.AwardList.Add(cmd);
        }

        return data;
    }

    private void Dirty() => IsDirty = true;

    private sealed class RunState(
        uint entryId,
        uint routeId,
        uint maxStep,
        uint step,
        WantedStepState current,
        List<(uint ProcessId, uint EventId)> history,
        List<uint> blesses,
        List<WantedBionics> bionics,
        List<uint> relics,
        uint coinsTotal,
        uint reviveCount,
        (uint Id, int X, int Y, int Z)? resetPoint,
        List<WantedAdventure> adventures,
        SortedDictionary<uint, uint> shopBuys
    )
    {
        public uint EntryId { get; } = entryId;
        public uint RouteId { get; } = routeId;
        public uint MaxStep { get; } = maxStep;
        public uint Step { get; set; } = step;
        public WantedStepState Current { get; set; } = current;
        public List<(uint ProcessId, uint EventId)> History { get; } = history;
        public List<uint> Blesses { get; } = blesses;
        public List<WantedBionics> Bionics { get; } = bionics;
        public uint LastBionicsId { get; set; } = bionics.Select(b => b.UniqId).DefaultIfEmpty().Max();
        public HashSet<uint> RedeemedSteps { get; } = [];
        public List<uint> Relics { get; } = relics;
        public uint CoinsTotal { get; set; } = coinsTotal;
        public uint ReviveCount { get; set; } = reviveCount;
        public (uint Id, int X, int Y, int Z)? ResetPoint { get; set; } = resetPoint;
        public List<WantedAdventure> Adventures { get; } = adventures;
        public SortedDictionary<uint, uint> ShopBuys { get; } = shopBuys;
    }
}
