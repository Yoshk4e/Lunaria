using Lunaria.Common.Tracking;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Wanted;

public sealed partial class WantedManager : TrackedObject
{
    private IReadOnlyList<WantedStepAward> BuildAwards(RunState run, List<ItemGrant> grants)
    {
        var process = assets.Wanted.StepsOf(run.RouteId, run.Step).Single(p => p.Id == run.Current.ProcessId);
        var choices = new List<WantedStepAward>();
        foreach (var definitionId in process.Award)
        {
            var definition = assets.Wanted.Award(definitionId)!;
            if (_random.Next(100) >= definition.Probability) continue;
            var rule = assets.Policy.Wanted.Awards[definitionId];
            var type = definition.Behavior;
            if (type is EWantedAwardType.AddCoinTypeA or EWantedAwardType.AddCoinTypeB)
            {
                if (rule.Amount > 0 && assets.Items.CurrencyItemFor((int)rule.Currency) is {} currency)
                    grants.Add(new ItemGrant(currency, rule.Amount));
                continue;
            }

            var candidates = Candidates(type, rule, run);
            var select = type is EWantedAwardType.AddBlessSelect or EWantedAwardType.AddRelicSelect or EWantedAwardType.AddCreatureSelect;
            var count = select ? assets.Policy.Wanted.SelectOptions : _random.Next(rule.MinCount, rule.MaxCount + 1);
            var drawn = Draw(candidates, count, type is EWantedAwardType.AddCreatureSelect or EWantedAwardType.AddOneCreature, run);
            if (drawn.Count == 0) continue;
            if (select)
            {
                Offer(type, drawn);
                continue;
            }
            foreach (var id in drawn)
            {
                switch (type)
                {
                    case EWantedAwardType.AddBlessRandom: run.Blesses.Add(id); break;
                    case EWantedAwardType.AddRelicRandom: run.Relics.Add(id); break;
                    case EWantedAwardType.AddOneCreature:
                        if (run.Bionics.Count >= assets.Wanted.CreatureMaxCount) Offer(EWantedAwardType.AddCreatureSelect, [id]);
                        else AddBionics(id);
                        break;
                    default: throw new InvalidOperationException($"Unsupported Wanted award behavior {type}");
                }
            }
        }
        return choices;

        void Offer(EWantedAwardType type, IReadOnlyList<uint> options) => choices.Add(new WantedStepAward(
            checked(run.Step * 65536 + (uint)choices.Count + 1), (uint)type, options, false));
    }

    private List<uint> Candidates(EWantedAwardType type, WantedAwardRule rule, RunState run)
    {
        IEnumerable<uint> ids = type switch {
            EWantedAwardType.AddBlessSelect or EWantedAwardType.AddBlessRandom => assets.Wanted.AllBlesses
                .Where(b => IsConditionMet(b.UnlockCondition) && Quality(b.BlessType) && !run.Blesses.Contains(b.Id)).Select(b => b.Id),
            EWantedAwardType.AddRelicSelect or EWantedAwardType.AddRelicRandom => assets.Wanted.AllRelics
                .Where(r => IsConditionMet(r.UnlockContion) && Quality(r.Quality) && !run.Relics.Contains(r.Id)).Select(r => r.Id),
            EWantedAwardType.AddCreatureSelect or EWantedAwardType.AddOneCreature => assets.Wanted.AllCreatures
                .Where(c => IsConditionMet(c.UnlockCondition) && Quality(c.Quality)).Select(c => c.Id),
            _ => throw new InvalidOperationException($"Unsupported Wanted award behavior {type}")
        };
        return ids.Where(id => (rule.Candidates.Length == 0 || rule.Candidates.Contains(id)) && !rule.Excluded.Contains(id)).Distinct().ToList();
        bool Quality(uint quality) => rule.Qualities.Length == 0 || rule.Qualities.Contains(quality);
    }

    private List<uint> Draw(List<uint> pool, int count, bool creatures, RunState run)
    {
        var drawn = new List<uint>();
        var weights = new Dictionary<uint, long>();
        if (creatures)
            foreach (var owned in run.Bionics)
            foreach (var modifier in assets.Wanted.Creature(owned.EntryId)!.CreatureWeightAmend)
            {
                var parts = modifier.Split('=');
                if (parts.Length == 2 && uint.TryParse(parts[0], out var id) && uint.TryParse(parts[1], out var boost))
                    weights[id] = checked(weights.GetValueOrDefault(id) + boost);
            }
        while (pool.Count > 0 && drawn.Count < count)
        {
            var total = pool.Sum(id => (long)assets.Policy.Wanted.CreatureBaseWeight + weights.GetValueOrDefault(id));
            var roll = _random.NextInt64(total);
            var index = 0;
            while (index < pool.Count - 1 && (roll -= assets.Policy.Wanted.CreatureBaseWeight + weights.GetValueOrDefault(pool[index])) >= 0) index++;
            drawn.Add(pool[index]);
            pool.RemoveAt(index);
        }
        return drawn;
    }

    private uint RedemptionStep => _run is not {} run ? 0 : run.Current.EventDone ? run.Step : run.Step - 1;

    public bool CanRedeem => _run is {} run && (!assets.Policy.Wanted.RedeemOncePerCompletedStep
        || RedemptionStep > 0 && !run.RedeemedSteps.Contains(RedemptionStep));

    public void MarkRedeemed()
    {
        if (!CanRedeem) throw new InvalidOperationException("Wanted redemption is not eligible");
        _run!.RedeemedSteps.Add(RedemptionStep);

    }
}
