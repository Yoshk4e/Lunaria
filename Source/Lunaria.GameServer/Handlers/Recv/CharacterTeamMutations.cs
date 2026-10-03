using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public static class CharacterTeamMutations
{
    public sealed class UpdateTeam
    {
        [RequireLogin]
        [GameHandler(EClientServerCmds.CsCharacterUpdateTeam)]
        public Task<SCCharacterUpdateTeam> OnPacket(NetContext ctx, CSCharacterUpdateTeam req) =>
            Task.FromResult(ctx.Player.UpdateTeam(req.TeamData));
    }

    public sealed class UpdateTeamName
    {
        [RequireLogin]
        [GameHandler(EClientServerCmds.CsCharacterUpdateTeamName)]
        public Task<SCCharacterUpdateTeam> OnPacket(NetContext ctx, CSCharacterUpdateTeamName req) =>
            Task.FromResult(ctx.Player.RenameTeam(req.TeamId, req.Name.ToStringUtf8()));
    }

    public sealed class SwitchTeam
    {
        [RequireLogin]
        [GameHandler(EClientServerCmds.CsCharacterSwitchTeam)]
        public Task<SCCharacterSwitchTeam> OnPacket(NetContext ctx, CSCharacterSwitchTeam req) =>
            Task.FromResult(ctx.Player.SwitchTeam(req.TeamId));
    }

    public sealed class SwitchMember
    {
        [RequireLogin]
        [GameHandler(EClientServerCmds.CsCharacterSwitchMember)]
        public Task<SCCharacterSwitchMember> OnPacket(NetContext ctx, CSCharacterSwitchMember req) =>
            Task.FromResult(ctx.Player.SwitchTeamMember(req.MemberSlot));
    }

    public sealed class SwitchMainCharacter
    {
        [RequireLogin]
        [GameHandler(EClientServerCmds.CsSwitchMainCharacter)]
        public Task<SCSwitchMainCharacter> OnPacket(NetContext ctx, CSSwitchMainCharacter req) =>
            Task.FromResult(ctx.Player.SwitchMainCharacter(req.TeamData, req.UsingMemberSlot));
    }
}
