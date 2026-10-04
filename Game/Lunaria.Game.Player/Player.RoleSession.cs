using Lunaria.Game.Player.Persistence;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public bool HasActiveRole => Account.IsBound && Roles.HasActive;

    public Player CreateRoleSession()
    {
        var next = new Player(SessionId, Assets, Time, RandomSources);
        if (Account.Id is {} accountId)
        {
            next.Account.Bind(accountId, Account.AccountKey, Account.Userid, Account.BoundAt);
            next.Account.SetOrigin(Account.ChannelName, Account.Udid);
            next.Roles.Load(Roles.All.Select(role => new RoleRow(role.Id, accountId, role.Slot,
                role.Name, role.SecondName, role.Gender, role.Initialized)));
        }
        return next;
    }

    public void InitializeRoleState(DateTimeOffset now, bool hasSave = false)
    {
        using var operationTime = BeginOperation(now);
        foreach (var motive in Motives.All.ToArray())
            if (motive.EquipedTarget != 0 && Characters.Get(motive.EquipedTarget)?.MotiveUniqId != motive.UniqId)
                Motives.ForceClearEquip(motive.UniqId);
        foreach (var character in Characters.All.ToArray())
            if (character.MotiveUniqId != 0 && Motives.Get(character.MotiveUniqId)?.EquipedTarget != character.InstId)
                Characters.ForceClearMotiveSlot(character.InstId);

        Guid.Adopt(new[] { Guid.LastMinted,
            Motives.All.Select(m => m.UniqId).DefaultIfEmpty().Max(),
            Mails.Entries.Select(m => (ulong)m.MailId).DefaultIfEmpty().Max() }.Max());
        TasksBootstrapped = false;
        Tasks.EnsureStarted();
        Cases.EnsureStarted();
        InstallQuestGates();
        UnlockWalkedGuides();
        // An empty saved bag or wallet may already have been spent.
        if (!hasSave) GrantStarterState();
        else
        {
            Characters.GrantStarter(Guid);
            Teams.GrantStarter(Characters);
            Skills.GrantStarter(Characters);
        }
        ApplyUnlockedTalentSkills();
        Mails.SweepExpired(now.ToUnixTimeSeconds());
        Progress.Regenerate(now);
        RetryStoredCreatures();
        RetryStoredMotives(now);
        CompleteRoleLogin();
    }
}
