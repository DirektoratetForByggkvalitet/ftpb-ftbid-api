using Ftpb.Ftbid.Application.Matrikkel;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ftpb.Ftbid.Api.Autentisering;
using Ftpb.Ftbid.Domain.Referansedata;
using Ftpb.Ftbid.Infrastructure.Persistens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Xunit;

namespace Ftpb.Ftbid.Tests.Oppsett;

/// <summary>Samler tester som deler en isolert PostgreSQL-database.</summary>
[CollectionDefinition("Postgres")]
public sealed class PostgresSamling : ICollectionFixture<PostgresApiOppsett>;

/// <summary>Starter API-et med ekte JWT-validering og en ny database for hver testkjøring.</summary>
public sealed class PostgresApiOppsett : IAsyncLifetime
{
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly string _databasenavn = "ftbid_test_" + Guid.NewGuid().ToString("N");
    private string? _tidligereTilkobling;
    private string _administrasjon = "";
    public string Tilkobling { get; private set; } = "";
    public WebApplicationFactory<Program> Fabrikk { get; private set; } = null!;
    public const string Utsteder = "https://test.maskinporten.no/";

    /// <summary>Oppretter en isolert testdatabase og fyller nødvendig kommuneregister og skjemametadata.</summary>
    public async Task InitializeAsync()
    {
        var grunnlag = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FTBID_TEST_POSTGRES") ??
            "Host=localhost;Port=55432;Database=postgres;Username=ftbid;Password=ftbid_lokal");
        grunnlag.Database = "postgres";
        _administrasjon = grunnlag.ConnectionString;
        await using (var forbindelse = new NpgsqlConnection(_administrasjon))
        {
            await forbindelse.OpenAsync();
            await using var kommando = new NpgsqlCommand($"CREATE DATABASE \"{_databasenavn}\"", forbindelse);
            await kommando.ExecuteNonQueryAsync();
        }
        grunnlag.Database = _databasenavn;
        Tilkobling = grunnlag.ConnectionString;
        _tidligereTilkobling = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", Tilkobling);
        Fabrikk = new TestApiFabrikk(new RsaSecurityKey(_rsa) { KeyId = "testnokkel" });
        _ = Fabrikk.CreateClient();
        using var omfang = Fabrikk.Services.CreateScope();
        var database = omfang.ServiceProvider.GetRequiredService<FtbidDbContext>();
        await database.Database.MigrateAsync();
        database.Kommuner.AddRange(new Kommune { Kommunenummer = "0301" }, new Kommune { Kommunenummer = "5001" },
            new Kommune { Kommunenummer = "9998", GyldigTil = DateTimeOffset.UtcNow.AddDays(-1) });
        database.Skjemametadata.Add(new Skjemametadata { Arkivreferanse = "AR123456789", Skjematype = "Søknad om rammetillatelse", Tiltakstyper = ["nyttanlegg"] });
        await database.SaveChangesAsync();
    }

    /// <summary>Fjerner kun databasen som dette testoppsettet selv opprettet.</summary>
    public async Task DisposeAsync()
    {
        if (Fabrikk is not null) await Fabrikk.DisposeAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _tidligereTilkobling);
        NpgsqlConnection.ClearAllPools();
        await using var forbindelse = new NpgsqlConnection(_administrasjon);
        await forbindelse.OpenAsync();
        await using var kommando = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databasenavn}\" WITH (FORCE)", forbindelse);
        await kommando.ExecuteNonQueryAsync();
        _rsa.Dispose();
    }

    /// <summary>Lager en HTTP-klient med et lokalt RSA-signert Maskinporten-lignende token.</summary>
    public HttpClient Klient(string scope = "dibk:bygg/ftbid.les dibk:bygg/ftbid.endre", string variant = "gyldig", string orgnummer = "123456789")
    {
        var klient = Fabrikk.CreateClient();
        if (variant == "mangler") return klient;
        var claims = new List<Claim> { new("scope", scope) };
        if (variant != "uten_konsument") claims.Add(new("consumer", variant == "ugyldig_konsument" ? "[]" : $"{{\"ID\":\"0192:{orgnummer}\"}}"));
        var nokkel = new RsaSecurityKey(_rsa) { KeyId = "testnokkel" };
        using var annenRsa = RSA.Create(2048);
        var signering = new SigningCredentials(variant == "feil_signatur" ? new RsaSecurityKey(annenRsa) { KeyId = "testnokkel" } : nokkel, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(variant == "feil_utsteder" ? "https://feil.no/" : Utsteder,
            claims: claims, notBefore: DateTime.UtcNow.AddHours(-1),
            expires: variant == "utlopt" ? DateTime.UtcNow.AddMinutes(-5) : DateTime.UtcNow.AddMinutes(5), signingCredentials: signering);
        klient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return klient;
    }

    private sealed class TestApiFabrikk(RsaSecurityKey nokkel) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder bygger)
        {
            bygger.UseEnvironment("Test");
            bygger.ConfigureServices(services =>
            {
                services.RemoveAll<IMatrikkelKlient>();
                services.AddSingleton<IMatrikkelKlient, TestMatrikkelKlient>();
            });
            bygger.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(MaskinportenAutentisering.Ordning, valg =>
            {
                var konfigurasjon = new OpenIdConnectConfiguration { Issuer = Utsteder };
                konfigurasjon.SigningKeys.Add(nokkel);
                valg.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(konfigurasjon);
            }));
        }
    }
}
