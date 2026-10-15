using Azure.Core;
using Azure.Identity;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using Microsoft.Azure.PostgreSQL.Auth;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ftpb.Ftbid.Infrastructure.Persistens;

/// <summary>Felles PostgreSQL-oppsett med valgfri passordløs Entra ID-autentisering.</summary>
public static class PostgresOppsett
{
    /// <summary>Konfigurerer EF Core og lar Npgsql eie og gjenbruke datakilden og tilkoblingspoolen.</summary>
    public static void Konfigurer(DbContextOptionsBuilder valg, string tilkobling, bool brukEntraId = false,
        TokenCredential? legitimasjon = null)
    {
        if (!brukEntraId)
        {
            valg.UseNpgsql(tilkobling);
            return;
        }

        var innstillinger = new NpgsqlConnectionStringBuilder(tilkobling);
        if (string.IsNullOrWhiteSpace(innstillinger.Host) || string.IsNullOrWhiteSpace(innstillinger.Database))
            throw new KonfigurasjonManglerException("ConnectionStrings:Postgres må angi Host og Database ved Entra ID");
        if (innstillinger.Password is not null || innstillinger.Passfile is not null)
            throw new KonfigurasjonManglerException("ConnectionStrings:Postgres må være uten Password og Passfile ved Entra ID");
        if (innstillinger.SslMode is not (SslMode.Require or SslMode.VerifyCA or SslMode.VerifyFull))
            throw new KonfigurasjonManglerException("ConnectionStrings:Postgres må bruke SSL Mode=Require, VerifyCA eller VerifyFull ved Entra ID");

        // Innstillingene er faste for prosessen. Npgsql kjører callbacken én gang per intern datakilde,
        // og Microsoft-biblioteket henter brukernavn og fornyer token ved nye tilkoblinger.
        valg.UseNpgsql(tilkobling, postgres => postgres.ConfigureDataSource(datakilde =>
            datakilde.UseEntraAuthentication(legitimasjon ?? new DefaultAzureCredential())));
    }
}
