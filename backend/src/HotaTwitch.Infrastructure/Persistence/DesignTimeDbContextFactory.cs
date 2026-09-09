using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HotaTwitch.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build the model without the API's environment configuration. The
/// connection string here is never opened; migrations only need the SQLite provider's shape.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HotaTwitchDbContext>
{
    public HotaTwitchDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<HotaTwitchDbContext>().UseSqlite("Data Source=hota-design-time.db").Options);
}
