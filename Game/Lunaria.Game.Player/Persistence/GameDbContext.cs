using Lunaria.Game.Player.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lunaria.Game.Player.Persistence;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RoleCharacter> RoleCharacters => Set<RoleCharacter>();
    public DbSet<RoleMotive> RoleMotives => Set<RoleMotive>();
    public DbSet<RoleGuide> RoleGuides => Set<RoleGuide>();
    public DbSet<RoleMail> RoleMails => Set<RoleMail>();
    public DbSet<RoleSave> RoleSaves => Set<RoleSave>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity => {
            entity.ToTable("accounts");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(a => a.AccountKey).HasColumnName("account_key").IsRequired();
            entity.HasIndex(a => a.AccountKey).IsUnique();
            entity.Property(a => a.Userid).HasColumnName("userid").IsRequired();
            entity.HasIndex(a => a.Userid).IsUnique();
            entity.Property(a => a.ChannelName).HasColumnName("channel_name");
            entity.Property(a => a.ChannelUid).HasColumnName("channel_uid");
            entity.Property(a => a.Udid).HasColumnName("udid");
            entity.Property(a => a.CreatedAt).HasColumnName("created_at");
            entity.Property(a => a.LastLoginAt).HasColumnName("last_login_at");
        });

        modelBuilder.Entity<Role>(entity => {
            entity.ToTable("roles");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(r => r.AccountId).HasColumnName("account_id");
            entity.Property(r => r.Slot).HasColumnName("slot");
            entity.Property(r => r.Name).HasColumnName("name").HasColumnType("TEXT COLLATE NOCASE");
            entity.Property(r => r.SecondName).HasColumnName("second_name");
            entity.Property(r => r.Gender).HasColumnName("gender");
            entity.Property(r => r.Initialized).HasColumnName("initialized");
            entity.Property(r => r.LastMintedInstId).HasColumnName("last_minted_inst_id");
            entity.Property(r => r.CreatedAt).HasColumnName("created_at");
            entity.Property(r => r.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(r => r.AccountId);
            entity.HasIndex(r => new { r.AccountId, r.Slot }).IsUnique();
            // The partial index allows duplicate placeholder names until naming is complete.
            entity.HasIndex(r => r.Name).IsUnique().HasFilter("initialized = 1");

            entity.HasOne(r => r.Account)
                .WithMany(a => a.Roles)
                .HasForeignKey(r => r.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoleCharacter>(entity => {
            entity.ToTable("role_characters");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(c => c.RoleId).HasColumnName("role_id");
            entity.Property(c => c.InstId).HasColumnName("inst_id");
            entity.Property(c => c.CharacterId).HasColumnName("character_id");
            entity.Property(c => c.Level).HasColumnName("level");
            entity.Property(c => c.Exp).HasColumnName("exp");
            entity.Property(c => c.BreakLevel).HasColumnName("break_level");
            entity.Property(c => c.MotiveUniqId).HasColumnName("motive_uniq_id");
            entity.HasIndex(c => c.RoleId);
            entity.HasIndex(c => new { c.RoleId, c.InstId }).IsUnique();

            entity.HasOne(c => c.Role)
                .WithMany()
                .HasForeignKey(c => c.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoleMotive>(entity => {
            entity.ToTable("role_motives");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(m => m.RoleId).HasColumnName("role_id");
            entity.Property(m => m.UniqId).HasColumnName("uniq_id");
            entity.Property(m => m.MotiveId).HasColumnName("motive_id");
            entity.Property(m => m.ItemId).HasColumnName("item_id");
            entity.Property(m => m.ClaimTime).HasColumnName("claim_time");
            entity.Property(m => m.Level).HasColumnName("level");
            entity.Property(m => m.Exp).HasColumnName("exp");
            entity.Property(m => m.RefineLevel).HasColumnName("refine_level");
            entity.Property(m => m.BreakLevel).HasColumnName("break_level");
            entity.Property(m => m.Locked).HasColumnName("locked");
            entity.Property(m => m.EquipedTarget).HasColumnName("equiped_target");
            entity.HasIndex(m => m.RoleId);
            entity.HasIndex(m => new { m.RoleId, m.UniqId }).IsUnique();

            entity.HasOne(m => m.Role)
                .WithMany()
                .HasForeignKey(m => m.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoleMail>(entity => {
            entity.ToTable("role_mails");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(m => m.RoleId).HasColumnName("role_id");
            entity.Property(m => m.MailId).HasColumnName("mail_id");
            entity.Property(m => m.TemplateId).HasColumnName("template_id");
            entity.Property(m => m.Type).HasColumnName("type");
            entity.Property(m => m.Important).HasColumnName("important");
            entity.Property(m => m.Open).HasColumnName("open");
            entity.Property(m => m.HasAttach).HasColumnName("has_attach");
            entity.Property(m => m.Items).HasColumnName("items");
            entity.Property(m => m.Time).HasColumnName("time");
            entity.Property(m => m.ExpireTime).HasColumnName("expire_time");
            entity.HasIndex(m => m.RoleId);
            entity.HasIndex(m => new { m.RoleId, m.MailId }).IsUnique();

            entity.HasOne(m => m.Role)
                .WithMany()
                .HasForeignKey(m => m.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoleGuide>(entity => {
            entity.ToTable("role_guides");
            entity.HasKey(g => g.RoleId);
            entity.Property(g => g.RoleId).HasColumnName("role_id");
            entity.Property(g => g.Entries).HasColumnName("entries");
            entity.Property(g => g.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(g => g.Role)
                .WithMany()
                .HasForeignKey(g => g.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoleSave>(entity => {
            entity.ToTable("role_saves");
            entity.HasKey(s => s.RoleId);
            entity.Property(s => s.RoleId).HasColumnName("role_id");
            entity.Property(s => s.State).HasColumnName("state").IsRequired();
            entity.Property(s => s.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(s => s.Role)
                .WithMany()
                .HasForeignKey(s => s.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
