using Ftpb.Ftbid.Domain.Errors.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ftpb.Ftbid.Infrastructure.Persistence;

/// <summary>Oppretter en databasekontekst for migrasjonsverktøy uten å starte API-et.</summary>
public sealed class FtbidDbContextFactory : IDesignTimeDbContextFactory<FtbidDbContext>
{
    /// <summary>Bruker ConnectionStrings__Postgres og Database__BrukEntraId, med lokal standard uten Entra ID.</summary>
    public FtbidDbContext CreateDbContext(string[] args)
    {
        var useEntraId = bool.Parse(Environment.GetEnvironmentVariable("Database__BrukEntraId") ?? "false");
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (useEntraId) throw new MissingConfigurationException("ConnectionStrings:Postgres");
            connectionString = "Host=localhost;Port=55432;Database=ftbid;Username=ftbid;Password=ftbid_lokal";
        }
        var options = new DbContextOptionsBuilder<FtbidDbContext>();
        PostgresConfiguration.Configure(options, connectionString, useEntraId);
        return new(options.Options);
    }
}
