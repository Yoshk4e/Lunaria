using Lunaria.Game.Player.Managers;
using Lunaria.Game.Player.Persistence;
using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleCreateRole(RoleRepository repo, ILogger<HandleCreateRole> logger)
{
    [GameHandler(EClientServerCmds.CsCreateRole)]
    public async Task<SCCreateRole> OnPacket(NetContext ctx, CSCreateRole req)
    {
        SCCreateRole Reject(int code)
        {
            return new SCCreateRole { Result = code };
        }

        if (ctx.Player.Account.Id is not {} accountId)
            return Reject((int)EnmTextCode.EnmTextNotAccLogin);

        if (ctx.Player.Roles.AtCap)
            return Reject((int)EnmTextCode.EnmTextCreateRoleRetInitRoleDataFail);

        if (RoleManager.ValidatePlaceholder(req.RoleName.Span, out var name) is {} nameError)
            return Reject(nameError);

        if (RoleManager.ValidatePlaceholder(req.SecondRoleName.Span, out var second) is {} secondError)
            return Reject(secondError);

        var slot = ctx.Player.Roles.NextSlot();

        try
        {
            var row = await repo.CreateAsync(accountId, slot, name!, second!).ConfigureAwait(false);
            var role = ctx.Player.Roles.Adopt(row);

            return new SCCreateRole {
                Result = 0,
                BriefRole = new RoleListBriefInfo { RoleId = (ulong)role.Id }
            };
        }
        catch (RoleRepositoryException ex)
        {
            return ex.Error switch {
                RoleRepositoryError.NameTaken => Reject((int)EnmTextCode.EnmTextCreateRoleRetNameExist),
                RoleRepositoryError.CapReached => Reject((int)EnmTextCode.EnmTextCreateRoleRetInitRoleDataFail),
                _ => Reject((int)EnmTextCode.EnmTextCreateRoleRetInsertRoleDataFail)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "role insert failed");
            return Reject((int)EnmTextCode.EnmTextCreateRoleRetInsertRoleDataFail);
        }
    }
}
