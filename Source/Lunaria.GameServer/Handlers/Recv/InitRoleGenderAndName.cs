using Lunaria.Game.Player.Managers;
using Lunaria.Game.Player.Persistence;
using Lunaria.Game.Player.Text;
using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleInitRoleGenderAndName(
    RoleRepository repo,
    ILogger<HandleInitRoleGenderAndName> logger
)
{
    [GameHandler(EClientServerCmds.CsInitRoleGenderAndName)]
    public async Task<SCInitRoleGenderAndName> OnPacket(
        NetContext ctx,
        CSInitRoleGenderAndName req
    )
    {
        SCInitRoleGenderAndName Echo(NamingOutcome outcome)
        {
            return new SCInitRoleGenderAndName {
                Gender = req.Gender,
                GenderResult = outcome.GenderResult,
                RoleName = req.RoleName,
                RoleNameResult = outcome.RoleNameResult,
                SecondRoleName = req.SecondRoleName,
                SecondRoleNameResult = outcome.SecondRoleNameResult
            };
        }

        if (!ctx.Player.IsLoggedIn || ctx.Player.Roles.IsEmpty)
            return Echo(NamingOutcome.Rejected((int)EnmTextCode.EnmTextNotAccLogin));

        var candidates = new[] {
            TextValidator.ValidatePlayerName(req.RoleName.Span) is null ? TextValidator.AsUtf8(req.RoleName.Span) : null,
            TextValidator.ValidatePlayerName(req.SecondRoleName.Span) is null ? TextValidator.AsUtf8(req.SecondRoleName.Span) : null
        };

        var taken = new List<string>();

        foreach (var candidate in candidates.OfType<string>())
        {
            try
            {
                if (await repo.NameTakenAsync(candidate).ConfigureAwait(false))
                    taken.Add(candidate);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "name availability check failed");
                return Echo(NamingOutcome.Rejected((int)EnmTextCode.EnmTextCreateRoleRetInitRoleDataFail));
            }
        }

        var outcome = ctx.Player.Roles.ApplyNaming(
            (int)req.Gender, req.RoleName.Span, req.SecondRoleName.Span, name => taken.Contains(name));

        if (outcome.AllOk)
            logger.LogInformation("role {RoleId} named", ctx.Player.Roles.Active()?.Id);

        return Echo(outcome);
    }
}
