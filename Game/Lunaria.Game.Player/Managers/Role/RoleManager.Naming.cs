using System.Diagnostics.CodeAnalysis;
using Lunaria.Game.Player.Text;
using Msg;

namespace Lunaria.Game.Player.Managers;

public sealed partial class RoleManager
{
    public static int? ValidatePlaceholder(ReadOnlySpan<byte> raw, [NotNullWhen(true)] out string? name)
    {
        name = TextValidator.AsUtf8(raw);

        if (name is null || name.Length == 0)
            return (int)EnmTextCode.EnmTextIllegalRoleName;

        if (name.Length > (int)EnmSizeLimit.MaxNameLen)
            return (int)EnmTextCode.EnmTextRoleNameTooLong;

        return null;
    }

    /// <summary>Check every field before making changes. The client needs all three result codes.</summary>
    public NamingOutcome ApplyNaming(
        int gender,
        ReadOnlySpan<byte> roleName,
        ReadOnlySpan<byte> secondRoleName,
        Func<string, bool> nameTaken
    )
    {
        if (Target() is not {} target)
            return NamingOutcome.Rejected((int)EnmTextCode.EnmTextNotAccLogin);

        if (target.Initialized)
            return NamingOutcome.Rejected((int)EnmTextCode.EnmTextRoleNameSetted);

        var genderResult = gender is (int)EnmGender.Male or (int)EnmGender.Female ? 0 : (int)EnmTextCode.EnmTextGenderParamWrong;

        int nameResult = 0, secondResult = 0;
        string? name = null, second = null;

        if (TextValidator.ValidatePlayerName(roleName) is {} nameCode)
            nameResult = nameCode;
        else
            name = TextValidator.AsUtf8(roleName);

        if (TextValidator.ValidatePlayerName(secondRoleName) is {} secondCode)
            secondResult = secondCode;
        else
            second = TextValidator.AsUtf8(secondRoleName);

        if (genderResult != 0 || nameResult != 0 || secondResult != 0)
            return new NamingOutcome(genderResult, nameResult, secondResult);

        var exists = (int)EnmTextCode.EnmTextCreateRoleRetNameExist;

        if (nameTaken(name!))
            return new NamingOutcome(genderResult, exists, secondResult);

        if (nameTaken(second!))
            return new NamingOutcome(genderResult, nameResult, exists);

        Replace(target with {
            Gender = gender,
            Name = name!,
            SecondName = second!,
            Initialized = true
        });

        return NamingOutcome.Ok;
    }

    /// <summary>Auto-creation can send naming before CS_ROLE_LOGIN. Use the newest role in that case.</summary>
    private RoleState? Target() => Active() ?? Newest();
}
