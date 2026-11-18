using System;
using System.Threading;
using System.Threading.Tasks;
using Ftpb.Ftbid.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ftpb.Ftbid.Api.HealthChecks;

/// <summary>Kontrollerer at PostgreSQL er tilgjengelig og databaseskjemaet er migrert.</summary>
public sealed class PostgresHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    /// <summary>Tester databasetilkoblingen og ftbid-tabellen.</summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<FtbidDbContext>();
            await database.Ftbider.AnyAsync(cancellationToken);
            return HealthCheckResult.Healthy("PostgreSQL er tilgjengelig.");
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL er utilgjengelig eller mangler databaseskjema.");
        }
    }
}
