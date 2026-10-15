using Ftpb.Ftbid.Domain.Feil.Unntak;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ftpb.Ftbid.Infrastructure.Persistens;

/// <summary>Oppretter en databasekontekst for migrasjonsverktøy uten å starte API-et.</summary>
public sealed class FtbidDbContextFabrikk : IDesignTimeDbContextFactory<FtbidDbContext>
{
    /// <summary>Bruker ConnectionStrings__Postgres og Database__BrukEntraId, med lokal standard uten Entra ID.</summary>
    public FtbidDbContext CreateDbContext(string[] argumenter)
    {
        var brukEntraId = bool.Parse(Environment.GetEnvironmentVariable("Database__BrukEntraId") ?? "false");
        var tilkobling = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        if (string.IsNullOrWhiteSpace(tilkobling))
        {
            if (brukEntraId) throw new KonfigurasjonManglerException("ConnectionStrings:Postgres");
            tilkobling = "Host=localhost;Port=55432;Database=ftbid;Username=ftbid;Password=ftbid_lokal";
        }
        var valg = new DbContextOptionsBuilder<FtbidDbContext>();
        PostgresOppsett.Konfigurer(valg, tilkobling, brukEntraId);
        return new(valg.Options);
    }
}
