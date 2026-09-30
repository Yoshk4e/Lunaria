using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public static class CharacterTeamMutations
{
    private const int NotReady = (int)EnmTextCode.EnmTextNotAccLogin;

    public sealed class UpdateTeam
    {
        [GameHandler(EClientServerCmds.CsCharacterUpdateTeam)]
        public Task<SCCharacterUpdateTeam> OnPacket(NetContext ctx, CSCharacterUpdateTeam req) =>
            Task.FromResult(ctx.Player.HasActiveRole ? ctx.Player.UpdateTeam(req.TeamData)
                : new SCCharacterUpdateTeam { Result = NotReady });
    }

    public sealed class UpdateTeamName
    {
        [GameHandler(EClientServerCmds.CsCharacterUpdateTeamName)]
        public Task<SCCharacterUpdateTeam> OnPacket(NetContext ctx, CSCharacterUpdateTeamName req) =>
            Task.FromResult(ctx.Player.HasActiveRole ? ctx.Player.RenameTeam(req.TeamId, req.Name.ToStringUtf8())
                : new SCCharacterUpdateTeam { Result = NotReady });
    }

    public sealed class SwitchTeam
    {
        [GameHandler(EClientServerCmds.CsCharacterSwitchTeam)]
        public Task<SCCharacterSwitchTeam> OnPacket(NetContext ctx, CSCharacterSwitchTeam req) =>
            Task.FromResult(ctx.Player.HasActiveRole ? ctx.Player.SwitchTeam(req.TeamId)
                : new SCCharacterSwitchTeam { Result = NotReady });
    }

    public sealed class SwitchMember
    {
        [GameHandler(EClientServerCmds.CsCharacterSwitchMember)]
        public Task<SCCharacterSwitchMember> OnPacket(NetContext ctx, CSCharacterSwitchMember req) =>
            Task.FromResult(ctx.Player.HasActiveRole ? ctx.Player.SwitchTeamMember(req.MemberSlot)
                : new SCCharacterSwitchMember { Result = NotReady });
    }

    public sealed class SwitchMainCharacter
    {
        [GameHandler(EClientServerCmds.CsSwitchMainCharacter)]
        public Task<SCSwitchMainCharacter> OnPacket(NetContext ctx, CSSwitchMainCharacter req) =>
            Task.FromResult(ctx.Player.HasActiveRole ? ctx.Player.SwitchMainCharacter(req.TeamData, req.UsingMemberSlot)
                : new SCSwitchMainCharacter { Result = NotReady });
    }
}
