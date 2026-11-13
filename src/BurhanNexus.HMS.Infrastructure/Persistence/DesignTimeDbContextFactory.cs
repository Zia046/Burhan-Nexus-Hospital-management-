using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BurhanNexus.HMS.Infrastructure.Persistence;

/// <summary>Used only by "dotnet ef" tooling to create migrations.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HmsDbContext>
{
    public HmsDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("HMS_DESIGN_CONNECTION")
                 ?? "Host=localhost;Port=5432;Database=burhan_nexus_hms;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<HmsDbContext>()
            .UseNpgsql(cs, o => o.MigrationsHistoryTable("__EFMigrationsHistory"))
            .Options;
        return new HmsDbContext(options);
    }
}
