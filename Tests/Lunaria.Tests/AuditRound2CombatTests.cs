using Lunaria.Game.Player;
using Lunaria.Game.Resources;
using Lunaria.Game.Resources.Tables;
using Lunaria.Game.Wanted;
using Msg;
using Xunit;
using Xunit.Abstractions;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
[Trait("Category", "AuditRound2")]
public sealed class AuditRound2CombatTests(BundledGameplayFixture fixture, ITestOutputHelper output)
{
    private GameData Assets => fixture.Data;

    private Player Fresh()
    {
        var player = new Player(1, Assets);
        player.Characters.GrantStarter(player.Guid);
        player.Teams.GrantStarter(player.Characters);
        return player;
    }

    private PWantedPosterNPC EnterWantedBattle(Player player)
    {
        Assert.Equal(0, player.EnterWanted(10101));
        // Pick a real first-step event in the current route. Some inferred pool
        // entries lack NPC rows, so the random initial event is not a stable fixture.
        var run = player.Wanted.CaptureRun()!;
        var process = Assets.Wanted.StepsOf(run.RouteId, 1).First();
        var eventId = Assets.Policy.Wanted.EventPools[process.Pool]
            .First(id => Assets.Wanted.Npc(id)?.NpcType == (uint)WantedNpcType.NormalBattle);
        player.Wanted.Load([], run with {
            Current = new WantedStepSnapshot(EnmWantedStepStatus.EnmWssStart, process.Id, eventId, false, [])
        });
        Assert.Equal(eventId, player.Wanted.CurrentEventId);
        return Assets.Wanted.Npc(eventId)!;
    }

    [Fact]
    public void CollectionWithGuaranteedDrops_CreditsRewardsBeforeDestroyingTheNode()
    {
        const uint collectionId = 202;
        var player = Fresh();
        var now = DateTimeOffset.UtcNow;
        var config = Assets.Collections.Get(collectionId)!;
        Assert.True(config.AutoDestroy);
        Assert.False(Assets.Collections.Respawn(collectionId).ResetsEver);
        // Chest 202 draws collection 13010 in its first group, including the original regression rewards.
        var expected = Assets.Collections.Rewards(13010, new Random(1));
        Assert.Equal(new[] { new ItemGrant(770004, 100), new ItemGrant(770002, 150) }, expected);
        var before = expected.ToDictionary(g => g.ItemId, g => player.OwnedItemCount(g.ItemId));
        var experienceBefore = player.Progress.TeamExp;
        var placed = Assets.Collections.WorldObjects(0).First(row => row.TemplateId == collectionId && row.CollectUnlockType == 0);
        Assert.Equal(0, player.Map.BeginEnter(placed.BlockId, 0).Code);
        Assert.Equal(0, player.Map.FinishEnter());
        var node = Assert.Single(player.GetCollections(placed.BlockId, now), n => n.UniqId == placed.Id);
        Assert.True(player.Map.SyncPosition((node.Location.X, node.Location.Y, node.Location.Z)));

        var result = player.Collect(node.UniqId, EnmCollectionOp.EnCollectionOpCollect, now);

        Assert.Equal(0, result.Code);
        Assert.NotNull(result.Outcome);
        Assert.Equal(EnmCollectionStatus.EcsDestroyed, result.Outcome.Item.Status);
        output.WriteLine($"Chest {collectionId}: collected and destroyed, credited {result.Outcome.Delivery.Credited.Count} reward lines.");
        foreach (var grant in expected)
        {
            var actual = player.OwnedItemCount(grant.ItemId) - before[grant.ItemId];
            output.WriteLine($"Item {grant.ItemId}: expected increase {grant.Count}, actual increase {actual}.");
        }
        foreach (var grant in expected)
        {
            if (Assets.Items.TeamExpOf(grant.ItemId) is {} perItem)
            {
                Assert.Equal(perItem * grant.Count, result.Outcome.Delivery.TeamExpFromItems);
                Assert.True(player.Progress.TeamExp > experienceBefore || player.Progress.TeamLevel > 1);
            }
            else
                Assert.Equal(before[grant.ItemId] + grant.Count, player.OwnedItemCount(grant.ItemId));
        }
    }

    [Fact]
    public void WantedDefeat_FreeRecoveryCannotBypassUnaffordablePaidRevival()
    {
        var player = Fresh();
        Assert.False(player.WantedRecover());
        var npc = EnterWantedBattle(player);
        var eventId = player.Wanted.CurrentEventId;
        Assert.Equal((uint)WantedNpcType.NormalBattle, npc.NpcType);
        Assert.True(player.Wanted.NextReviveCost() > 0);
        Assert.Equal(0, player.Wallet.Balance((int)MoneyType.ThoughtSand));
        var members = player.CurrentTeamMembers();
        Assert.NotEmpty(members);
        Assert.Equal(0, player.Battles.Enter(EBattleType.EnmBattleTypeWanted, npc.Params, 123, default));
        Assert.Equal(0, player.Battles.Start(EBattleType.EnmBattleTypeWanted, npc.Params));
        var report = new CSLeaveBattle {
            BattleType = EBattleType.EnmBattleTypeWanted,
            BattleFieldId = npc.Params,
            BattleInstId = 123,
            BattleResult = EBattleResultType.EnmBattleResultTypeDeadFail,
            CharacterData = { members.Select(id => new CharacterAttribInfo {
                InstId = id, CurrentHp = 0, PermanentLiquid = 0
            }) }
        };
        Assert.Equal(0, player.LeaveBattle(report).Result);
        Assert.All(members, id => Assert.Equal(0, player.TeamCharacterHp(id)));
        Assert.NotEqual(0, player.BuyWantedRevive(members));

        var recovered = player.WantedRecover();

        output.WriteLine($"Defeated wanted event {eventId} (battle NPC): paid revival unaffordable, free recovery accepted={recovered}, revive count={player.Wanted.ReviveCount}, ThoughtSand={player.Wallet.Balance((int)MoneyType.ThoughtSand)}.");
        output.WriteLine($"HP after free recovery: {string.Join(", ", members.Select(player.TeamCharacterHp))}.");
        Assert.False(recovered);
        Assert.All(members, id => Assert.Equal(0, player.TeamCharacterHp(id)));
    }

    [Fact]
    public void WantedBattleForAnotherField_CannotCompleteTheCurrentEvent()
    {
        var player = Fresh();
        var npc = EnterWantedBattle(player);
        var eventId = player.Wanted.CurrentEventId;
        Assert.Equal((uint)WantedNpcType.NormalBattle, npc.NpcType);
        const uint invalidField = uint.MaxValue;
        Assert.NotEqual(invalidField, npc.Params);
        // The production EnterBattle handler delegates directly to this manager.
        var entry = player.Battles.Enter(EBattleType.EnmBattleTypeWanted, invalidField, 123, default);
        if (entry != 0) return; // Rejecting the foreign field at entry also prevents the bug.
        Assert.Equal(0, player.Battles.Start(EBattleType.EnmBattleTypeWanted, invalidField));

        var result = player.LeaveBattle(new CSLeaveBattle {
            BattleType = EBattleType.EnmBattleTypeWanted,
            BattleFieldId = invalidField,
            BattleInstId = 123,
            BattleResult = EBattleResultType.EnmBattleResultTypeSuccess
        });

        output.WriteLine($"Wanted event {eventId} requires battlefield {npc.Params}, submitted {invalidField}: completed={result.WantedStepCompleted}, reward lines={result.WantedReward.Credited.Count}.");
        Assert.False(result.WantedStepCompleted);
        Assert.False(player.Wanted.IsEventComplete(eventId));
    }

    [Fact]
    public void WantedBattleForConfiguredField_CompletesTheCurrentEvent()
    {
        var player = Fresh();
        var npc = EnterWantedBattle(player);
        var eventId = player.Wanted.CurrentEventId;
        Assert.Equal(0, player.Battles.Enter(EBattleType.EnmBattleTypeWanted, npc.Params, 123, default));
        Assert.Equal(0, player.Battles.Start(EBattleType.EnmBattleTypeWanted, npc.Params));

        var result = player.LeaveBattle(new CSLeaveBattle {
            BattleType = EBattleType.EnmBattleTypeWanted,
            BattleFieldId = npc.Params,
            BattleInstId = 123,
            BattleResult = EBattleResultType.EnmBattleResultTypeSuccess
        });

        Assert.Equal(0, result.Result);
        Assert.True(result.WantedStepCompleted);
        Assert.True(player.Wanted.IsEventComplete(eventId));
    }

    [Fact]
    public void WantedPaidRevival_ConsumesSandAndAReviveAttempt()
    {
        var player = Fresh();
        EnterWantedBattle(player);
        var members = player.CurrentTeamMembers();
        foreach (var member in members) player.SetTeamCharacterVitals(member, hp: 0, liquid: 0);
        var cost = player.Wanted.NextReviveCost()!.Value;
        Assert.True(cost > 0);
        Assert.Equal(0, player.Wallet.Credit((int)MoneyType.ThoughtSand, cost));

        Assert.Equal(0, player.BuyWantedRevive(members));

        Assert.Equal(0, player.Wallet.Balance((int)MoneyType.ThoughtSand));
        Assert.Equal(1u, player.Wanted.ReviveCount);
        Assert.All(members, member => Assert.True(player.TeamCharacterHp(member) > 0));
    }

    [Fact]
    public void WantedRecovery_RequiresAnUnfinishedRecoveryEncounterAndNoActiveBattle()
    {
        var player = Fresh();
        var battle = EnterWantedBattle(player);
        var run = player.Wanted.CaptureRun()!;
        var recoveryProcess = Enumerable.Range(1, (int)run.MaxStep)
            .SelectMany(step => Assets.Wanted.StepsOf(run.RouteId, (uint)step))
            .First(process => Assets.Policy.Wanted.EventPools[process.Pool].Contains(8u));
        run = run with { Step = recoveryProcess.StepCount,
            Current = run.Current with { ProcessId = recoveryProcess.Id, EventId = 8, EventDone = false } };
        player.Wanted.Load([], run with {
            Current = run.Current with { EventDone = false }
        });
        Assert.Equal((uint)WantedNpcType.RecoverHp, Assets.Wanted.Npc(11)!.NpcType);
        var member = player.CurrentTeamMembers().First();
        player.SetTeamCharacterVitals(member, hp: 1);
        Assert.Equal(0, player.Battles.Enter(EBattleType.EnmBattleTypeWanted, battle.Params, 123, default));
        Assert.False(player.WantedRecover());
        Assert.Equal(1, player.TeamCharacterHp(member));
        Assert.Equal(0, player.LeaveBattle(new CSLeaveBattle {
            BattleType = EBattleType.EnmBattleTypeWanted, BattleFieldId = battle.Params,
            BattleInstId = 123, BattleResult = EBattleResultType.EnmBattleResultTypeDeadFail
        }).Result);
        Assert.True(player.WantedRecover());
        Assert.Equal(player.TeamCharacterMaxHp(member), player.TeamCharacterHp(member));
        player.Wanted.Load([], run with {
            Current = run.Current with { EventDone = true }
        });
        Assert.False(player.WantedRecover());
    }
}
