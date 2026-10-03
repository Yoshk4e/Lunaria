using Google.Protobuf;
using Google.Protobuf.Reflection;
using Msg;

namespace Lunaria.GameServer.Net;

internal static class LoginRejection
{
    private const int Code = (int)EnmTextCode.EnmTextNotAccLogin;

    public static Func<NetContext, object, ValueTask> Compile(Type? replyType)
    {
        if (replyType is null) return (_, _) => ValueTask.CompletedTask;
        if (!typeof(IMessage).IsAssignableFrom(replyType) || replyType.GetConstructor(Type.EmptyTypes) is null)
            throw new InvalidOperationException($"Login rejection type {replyType.Name} must be a protobuf message");

        var descriptor = ((IMessage)Activator.CreateInstance(replyType)!).Descriptor;
        var result = descriptor.FindFieldByName("result") ?? descriptor.FindFieldByName("ret");
        if (result is not null && (result.IsRepeated || result.FieldType is not (FieldType.Int32 or FieldType.UInt32)))
            throw new InvalidOperationException($"Login rejection status on {replyType.Name} must be int32 or uint32");

        return (ctx, request) => {
            var reply = Payload(ctx, (IMessage)request) ?? descriptor.Parser.ParseFrom(ByteString.Empty);
            if (reply.Descriptor != descriptor)
                throw new InvalidOperationException($"Login rejection for {request.GetType().Name} has the wrong response type");
            if (result is not null)
                result.Accessor.SetValue(reply, result.FieldType == FieldType.UInt32 ? (object)(uint)Code : Code);
            return ctx.SendRejectionAsync(reply);
        };
    }

    // The client expects these request IDs and nested messages even when login is rejected.
    // Other replies use the status code alone, or empty data when there is no status field.
    // this is probably definitly not the best idea.... feel free to criticize :3
    private static IMessage? Payload(NetContext ctx, IMessage request) => request switch {
        CSAchievementAddProgress req => new SCAchievementAddProgress { Id = req.Id },
        CSAchievementEvent req => new SCAchievementEvent { Event = req.Event, Args = { req.Args } },
        CSAchievementQuery => new SCAchievementQuery { Data = new CmdAchievementData() },
        CSAchievementReward req => new SCAchievementReward { AllIds = { req.AllIds } },
        CSBattlePassAward req => new SCBattlePassAward { Id = req.Id },
        CSBattlePassData req => new SCBattlePassData {
            Datas = { req.BattlePassId.Select(id => new CmdOneBattelPassData { Id = id }) }
        },
        CSCaseCluePut req => new SCCaseCluePut { ClueId = req.ClueId },
        CSCaseEvidenceDecrypted req => new SCCaseEvidenceDecrypted { EvidenceId = req.EvidenceId },
        CSCharacterLevelBreak req => new SCCharacterLevelBreak {
            CurrentData = ctx.Player.Characters.Get(req.InstId) is {} before ? ctx.Player.Characters.ToCharacterData(before) : null
        },
        CSCharacterLevelUp req => new SCCharacterLevelUp {
            CurrentData = ctx.Player.Characters.Get(req.InstId) is {} before ? ctx.Player.Characters.ToCharacterData(before) : null
        },
        CSCharacterTmpTeamQuery req => new SCCharacterTmpTeamQuery { TeamType = req.TeamType, TeamSrc = req.TeamSrc },
        CSCharacterUpdateTmpTeam req => new SCCharacterUpdateTmpTeam { TeamType = req.TeamType, TeamSrc = req.TeamSrc },
        CSChargeGoodsOrder req => new SCChargeGoodsOrder { GoodsId = req.GoodsId },
        CSCollectionOperate req => new SCCollectionOperate { CollectionItem = new OneCollectionData { UniqId = req.UniqId } },
        CSDailyMissionActivePointClaim req => new SCDailyMissionActivePointClaim {
            MissionItem = new DailyMissionItem { MissionId = req.MissionId }
        },
        CSDailyMissionQuery => new SCDailyMissionQuery {
            Data = new DailyMissionData { MissionListData = new DailyMissionListData(), RewardData = new DailyMissionRewardData() }
        },
        CSDailyMissionRewardClaim => new SCDailyMissionRewardClaim { RewardData = new DailyMissionRewardData() },
        CSDungeonsCurrentData req => new SCDungeonsCurrentData { Info = req.Info },
        CSDungeonsEnter req => new SCDungeonsEnter { DungeonsId = req.DungeonsId },
        CSDungeonsFullData => new SCDungeonsFullData {
            Data = new CSDungeonsData { CommonData = new CSDungeonsCommonData(), HordeData = new CSHordeData() }
        },
        CSEnterBattle req => new SCEnterBattle { BattleType = req.BattleType, BattleFieldId = req.BattleFieldId },
        CSStartBattle req => new SCStartBattle { BattleType = req.BattleType, BattleFieldId = req.BattleFieldId },
        CSPauseBattle req => new SCPauseBattle { BattleType = req.BattleType, BattleFieldId = req.BattleFieldId, Pause = req.Pause },
        CSLeaveBattle req => new SCLeaveBattle { BattleType = req.BattleType, BattleFieldId = req.BattleFieldId, BattleResult = req.BattleResult },
        CSFinEnterMap req => new SCFinEnterMap { RoleId = req.RoleId },
        CSInitRoleGenderAndName req => new SCInitRoleGenderAndName {
            Gender = req.Gender, GenderResult = Code,
            RoleName = req.RoleName, RoleNameResult = Code,
            SecondRoleName = req.SecondRoleName, SecondRoleNameResult = Code
        },
        CSItemUseCount req => new SCItemUseCount { ItemId = req.ItemId, Total = ctx.Player.InventoryCount(req.ItemId) },
        CSMailGetList req => new SCMailGetList { FromMailId = req.FromMailId, Count = req.Count },
        CSMailDel req => new SCMailDel { MailId = req.MailId },
        CSMailRead req => new SCMailRead { MailId = req.MailId },
        CSMailRecvAttachments req => new SCMailRecvAttachments { MailId = req.MailId },
        CSMotiveBreak req => new SCMotiveBreak { MotiveUniqId = req.MotiveUniqId },
        CSMotiveLevelUp req => new SCMotiveLevelUp { MotiveUniqId = req.MotiveUniqId },
        CSMotiveRefineUp req => new SCMotiveRefineUp { MotiveUniqId = req.MotiveUniqId },
        CSMotiveEquip req => new SCMotiveEquip { MotiveUniqId = req.MotiveUniqId, InstId = req.InstId },
        CSMotiveUnequip req => new SCMotiveUnequip { MotiveUniqId = req.MotiveUniqId, InstId = req.InstId },
        CSMotiveLock req => new SCMotiveLock { MotiveUniqId = req.MotiveUniqId, LockState = req.LockState },
        CSReqClaimReward req => new SCResClaimReward { SubRegionId = req.SubRegionId },
        CSRoleLogout req => new SCRoleLogout { RoleId = req.RoleId },
        CSSavePointUnlock req => new SCSavePointUnlock { SavepointId = req.SavepointId },
        CSSavePointSync req => new SCSavePointSync { SavepointId = req.SavepointId },
        CSReqUnlockTeleport req => new SCResUnlockTeleport { TeleportId = req.TeleportId },
        CSShopBuy req => new SCShopBuy { ShopId = req.ShopId },
        CSShopGoods req => new SCShopGoods { ShopId = req.ShopId },
        CSSignInActivityData req => new SCSignInActivityData { ActivityData = new SignInActivityData { ActivityId = req.ActivityId } },
        CSSignInActivityReward req => new SCSignInActivityReward { ActivityId = req.ActivityId, Day = req.Day },
        CSReqSilverCreatureInBattle req => new SCResSilverCreatureInBattleResult { UniqId = req.UniqId },
        CSReqSilverCreatureLeaveBattle req => new SCResSilverCreatureLeaveBattleResult { UniqId = req.UniqId },
        CSTalentUnlock req => new SCTalentUnlock { InstId = req.InstId, TalentNode = req.TalentNode },
        CSTaskActionUpdate req => new SCTaskActionUpdate { ActionId = req.ActionId, TaskType = req.TaskType, MaxProgress = 1 },
        CSWantedResetPoint req => new SCWantedResetPoint { Id = req.Id },
        _ => null
    };
}
