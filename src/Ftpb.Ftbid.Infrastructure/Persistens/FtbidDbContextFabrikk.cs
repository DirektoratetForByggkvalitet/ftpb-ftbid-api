using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ftpb.Ftbid.Infrastructure.Persistens;

/// <summary>Oppretter en databasekontekst for migrasjonsverktøy uten å starte API-et.</summary>
public sealed class FtbidDbContextFabrikk : IDesignTimeDbContextFactory<FtbidDbContext>
{
    /// <summary>Bruker miljøvariabelen ConnectionStrings__Postgres eller lokal utviklingsdatabase.</summary>
    public FtbidDbContext CreateDbContext(string[] argumenter)
    {
        var tilkobling = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres") ??
            "Host=localhost;Port=55432;Database=ftbid;Username=ftbid;Password=ftbid_lokal";
        return new(new DbContextOptionsBuilder<FtbidDbContext>().UseNpgsql(tilkobling).Options);
    }
}
