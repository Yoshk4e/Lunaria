using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Mail;

public readonly record struct MailClaim(int Code, uint MailId, IReadOnlyList<ItemGrant> Attachments)
{
    public bool Ok => Code == 0;

    public static MailClaim Granted(uint mailId, IReadOnlyList<ItemGrant> attachments) =>
        new(Code: 0, mailId, attachments);

    public static MailClaim Unknown(uint mailId) =>
        new((int)EnmTextCode.EnmTextWrongParam, mailId, []);

    public static MailClaim AlreadyReceived(uint mailId) =>
        new((int)EnmTextCode.EnmTextMailAttachmentAlreadyRecv, mailId, []);
}
