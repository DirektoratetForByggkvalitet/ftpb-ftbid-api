using Azure.Core;
using Azure.Identity;
using Ftpb.Ftbid.Domain.Errors.Exceptions;
using Microsoft.Azure.PostgreSQL.Auth;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ftpb.Ftbid.Infrastructure.Persistence;

/// <summary>Felles PostgreSQL-oppsett med valgfri passordløs Entra ID-autentisering.</summary>
public static class PostgresConfiguration
{
    /// <summary>Konfigurerer EF Core og lar Npgsql eie og gjenbruke datakilden og tilkoblingspoolen.</summary>
    public static void Configure(DbContextOptionsBuilder options, string connectionString, bool useEntraId = false,
        TokenCredential? credential = null)
    {
        if (!useEntraId)
        {
            options.UseNpgsql(connectionString);
            return;
        }

        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.Database))
            throw new MissingConfigurationException("ConnectionStrings:Postgres må angi Host og Database ved Entra ID");
        if (settings.Password is not null || settings.Passfile is not null)
            throw new MissingConfigurationException("ConnectionStrings:Postgres må være uten Password og Passfile ved Entra ID");
        if (settings.SslMode is not (SslMode.Require or SslMode.VerifyCA or SslMode.VerifyFull))
            throw new MissingConfigurationException("ConnectionStrings:Postgres må bruke SSL Mode=Require, VerifyCA eller VerifyFull ved Entra ID");

        // Innstillingene er faste for prosessen. Npgsql kjører callbacken én gang per intern datakilde,
        // og Microsoft-biblioteket henter brukernavn og fornyer token ved nye tilkoblinger.
        options.UseNpgsql(connectionString, postgres => postgres.ConfigureDataSource(dataSource =>
            dataSource.UseEntraAuthentication(credential ?? new DefaultAzureCredential())));
    }
}
