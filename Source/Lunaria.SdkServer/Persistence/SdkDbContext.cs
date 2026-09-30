using Microsoft.EntityFrameworkCore;

namespace Lunaria.SdkServer.Persistence;


public sealed class SdkDbContext(DbContextOptions<SdkDbContext> options) : DbContext(options)
{
    public DbSet<SdkUser> Users => Set<SdkUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SdkUser>(entity => {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).HasColumnName("id");
            entity.Property(u => u.Email).HasColumnName("email").IsRequired().HasMaxLength(255);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired().HasMaxLength(255);
            entity.Property(u => u.Nickname).HasColumnName("nickname").HasMaxLength(32);
            entity.Property(u => u.AvatarUrl).HasColumnName("avatar_url");
            entity.Property(u => u.Locked).HasColumnName("locked");
            entity.Property(u => u.FailedLoginAttempts).HasColumnName("failed_login_attempts");
            entity.Property(u => u.CreatedAt).HasColumnName("created_at");
            entity.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        });
    }
}
