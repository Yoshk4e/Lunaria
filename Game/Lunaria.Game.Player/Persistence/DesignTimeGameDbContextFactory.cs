using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Lunaria.Game.Player.Persistence;

public sealed class DesignTimeGameDbContextFactory : IDesignTimeDbContextFactory<GameDbContext>
{
    public GameDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite("Data Source=game_server.db")
            .Options);
}
