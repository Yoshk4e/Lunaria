using Lunaria.Common;
using Lunaria.Game.Characters;
using Lunaria.Game.Resources;
using Lunaria.Tests.Support;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection(AssetsCollection.Name)]
public sealed class CharacterManagerTests(TestAssets assets)
{
    private CharacterManager Manager() => new(assets.Data);

    private static (CharacterManager Manager, GuidManager Guid, ulong InstId) WithStarter(TestAssets assets)
    {
        var manager = new CharacterManager(assets.Data);
        var guid = new GuidManager();
        manager.GrantStarter(guid);
        return (manager, guid, manager.All[0].InstId);
    }

    [Fact]
    public void Add_MintsFreshInstance_AndRefusesUnknownOrDuplicate()
    {
        var manager = Manager();
        var guid = new GuidManager();

        var first = manager.Add(guid, TestAssets.CharacterId);
        Assert.True(first.Ok);
        Assert.Equal(expected: 1UL, first.InstId);
        Assert.Equal(Starter.CharacterLevel, manager.Get(first.InstId)!.Level);
        Assert.True(manager.IsDirty);

        var unknown = manager.Add(guid, TestAssets.UnknownCharacterId);
        Assert.Equal((int)EnmTextCode.EnmTextCharacterNotExist, unknown.Code);
        Assert.Equal(expected: 0UL, unknown.InstId);

        var duplicate = manager.Add(guid, TestAssets.CharacterId);
        Assert.Equal((int)EnmTextCode.EnmTextCharacterAlreadyExist, duplicate.Code);
        Assert.Equal(expected: 1, manager.Count);
    }

    [Fact]
    public void GrantStarter_FollowsTheTable_AndIsIdempotent()
    {
        var manager = Manager();
        var guid = new GuidManager();

        Assert.Equal(assets.Data.Starter.Characters.Count, manager.GrantStarter(guid));
        Assert.Equal(assets.Data.Starter.Characters, manager.All.Select(c => c.CharacterId).ToList());
        Assert.Equal(Starter.CharacterBreakLevel, manager.All[0].BreakLevel);

        Assert.Equal(expected: 0, manager.GrantStarter(guid));
        Assert.Equal(assets.Data.Starter.Characters.Count, manager.Count);
    }

    [Fact]
    public void Load_ReplacesRoster_DropsUnknowns_AndClampsToTheBreakCap()
    {
        var manager = Manager();
        manager.Add(new GuidManager(), TestAssets.CharacterId);

        manager.Load([
            new CharacterState { InstId = 5, CharacterId = TestAssets.CharacterId, Level = 99 },
            new CharacterState { InstId = 6, CharacterId = TestAssets.UnknownCharacterId, Level = 1 }
        ]);

        var single = Assert.Single(manager.All);
        Assert.Equal(expected: 5UL, single.InstId);
        Assert.Equal(TestAssets.CapAtBreakZero, single.Level);
        Assert.False(manager.IsDirty);
    }

    [Fact]
    public void GrantExp_ClimbsTheLadder_AndBanksTheRemainder()
    {
        var (manager, _, instId) = WithStarter(assets);

        var result = manager.GrantExp(instId, exp: 35);

        Assert.Equal(expected: 2u, result.LevelsGained);
        Assert.Equal(TestAssets.CapAtBreakZero, result.Level);
        Assert.Equal(expected: 0u, result.Exp);

        Assert.Equal(expected: 5u, result.Dropped);
        Assert.Equal(expected: 0, result.Code);
    }

    [Fact]
    public void GrantExp_BelowTheCap_KeepsLeftoverExperience()
    {
        var (manager, _, instId) = WithStarter(assets);

        var result = manager.GrantExp(instId, exp: 15);

        Assert.True(result.Ok);
        Assert.Equal(expected: 1u, result.LevelsGained);
        Assert.Equal(expected: 2u, result.Level);
        Assert.Equal(expected: 5u, result.Exp);
        Assert.Equal(expected: 0u, result.Dropped);
    }

    [Fact]
    public void GrantExp_AtTheCap_RefusesWithoutBanking()
    {
        var (manager, _, instId) = WithStarter(assets);
        manager.GrantExp(instId, exp: 30);
        Assert.Equal(TestAssets.CapAtBreakZero, manager.Get(instId)!.Level);

        var result = manager.GrantExp(instId, exp: 100);

        Assert.Equal((int)EnmTextCode.EnmTextCharacterExpFull, result.Code);
        Assert.Equal(expected: 100u, result.Dropped);
        Assert.Equal(expected: 0u, manager.Get(instId)!.Exp);
    }

    [Fact]
    public void GrantExp_UnknownInstance_IsRefused()
    {
        var manager = Manager();
        Assert.Equal((int)EnmTextCode.EnmTextCharacterNotExist, manager.GrantExp(instId: 42, exp: 10).Code);
    }

    [Fact]
    public void CheckBreak_WantsMaxLevelThenWorldLevel()
    {
        var (manager, _, instId) = WithStarter(assets);

        Assert.Equal((int)EnmTextCode.EnmTextCharacterLevelLimit, manager.CheckBreak(instId, worldLevel: 9));

        manager.GrantExp(instId, exp: 30);

        Assert.Equal(
            (int)EnmTextCode.EnmTextWorldLevelNotEnough,
            manager.CheckBreak(instId, TestAssets.BreakWorldLevel - 1));
        Assert.Equal(expected: 0, manager.CheckBreak(instId, TestAssets.BreakWorldLevel));
    }

    [Fact]
    public void ApplyBreak_RaisesTheCeiling_ThenRefusesWhenFullyBroken()
    {
        var (manager, _, instId) = WithStarter(assets);
        manager.GrantExp(instId, exp: 30);

        Assert.Equal(expected: 0, manager.ApplyBreak(instId, TestAssets.BreakWorldLevel));
        Assert.Equal(expected: 1u, manager.Get(instId)!.BreakLevel);
        Assert.Equal(TestAssets.CapAtBreakOne, manager.LevelCap(instId));

        var result = manager.GrantExp(instId, exp: 70);
        Assert.Equal(expected: 2u, result.LevelsGained);
        Assert.Equal(TestAssets.CapAtBreakOne, result.Level);

        Assert.True(manager.IsFullyBroken(instId));
        Assert.Equal((int)EnmTextCode.EnmTextCharacterStarFull, manager.CheckBreak(instId, worldLevel: 99));
    }

    [Fact]
    public void Attribs_FollowTheInstanceLevel()
    {
        var (manager, _, instId) = WithStarter(assets);
        var atLevelOne = manager.AttribData(instId).AttribData.ToList();

        manager.GrantExp(instId, exp: 10);
        var atLevelTwo = manager.AttribData(instId).AttribData.ToList();

        Assert.NotEmpty(atLevelOne);
        Assert.NotEqual(atLevelOne[0].FinalValue, atLevelTwo[0].FinalValue);
    }
}
