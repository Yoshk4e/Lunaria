using Microsoft.EntityFrameworkCore;

namespace Lunaria.SdkServer.Persistence;

public sealed class UserRepository(IDbContextFactory<SdkDbContext> factory)
{
    public async Task<SdkUser?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task IncrementAndLockIfOverAsync(string email, int threshold, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
            return;

        user.FailedLoginAttempts += 1;

        if (user.FailedLoginAttempts >= threshold)
            user.Locked = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task ResetFailedAttemptsAsync(string email, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
            return;

        user.FailedLoginAttempts = 0;
        await db.SaveChangesAsync(ct);
    }

    public async Task<SdkUser> CreateAsync(string email, string passwordHash, string? nickname, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var user = new SdkUser {
            Id = Guid.CreateVersion7(),
            Email = email,
            PasswordHash = passwordHash,
            Nickname = nickname,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task UpdateLastLoginAsync(Guid userId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return;

        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
