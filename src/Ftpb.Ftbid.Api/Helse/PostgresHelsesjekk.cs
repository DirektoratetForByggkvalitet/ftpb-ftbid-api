using System;
using System.Threading;
using System.Threading.Tasks;
using Ftpb.Ftbid.Infrastructure.Persistens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ftpb.Ftbid.Api.Helse;

/// <summary>Kontrollerer at PostgreSQL er tilgjengelig og databaseskjemaet er migrert.</summary>
public sealed class PostgresHelsesjekk(IServiceScopeFactory omfangsfabrikk) : IHealthCheck
{
    /// <summary>Tester databasetilkoblingen og ftbid-tabellen.</summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext kontekst, CancellationToken avbrudd = default)
    {
        try
        {
            using var omfang = omfangsfabrikk.CreateScope();
            var database = omfang.ServiceProvider.GetRequiredService<FtbidDbContext>();
            await database.Ftbider.AnyAsync(avbrudd);
            return HealthCheckResult.Healthy("PostgreSQL er tilgjengelig.");
        }
        catch (Exception feil) when (feil is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL er utilgjengelig eller mangler databaseskjema.");
        }
    }
}
