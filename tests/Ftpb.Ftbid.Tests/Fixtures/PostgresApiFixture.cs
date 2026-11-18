using Ftpb.Ftbid.Api.Authentication;
using Ftpb.Ftbid.Domain.Referansedata;
using Ftpb.Ftbid.Infrastructure.Persistence;
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

namespace Ftpb.Ftbid.Tests.Fixtures;

/// <summary>Samler tester som deler en isolert PostgreSQL-database.</summary>
[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresApiFixture>;

/// <summary>Starter API-et med ekte JWT-validering og en ny database for hver testkjøring.</summary>
public sealed class PostgresApiFixture : IAsyncLifetime
{
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly string _databaseName = "ftbid_test_" + Guid.NewGuid().ToString("N");
    private string? _previousConnectionString;
    private string _adminConnectionString = "";
    public string ConnectionString { get; private set; } = "";
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public const string Issuer = "https://test.maskinporten.no/";

    /// <summary>Oppretter en isolert testdatabase og fyller nødvendig kommuneregister og skjemametadata.</summary>
    public async Task InitializeAsync()
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FTBID_TEST_POSTGRES") ??
            "Host=localhost;Port=55432;Database=postgres;Username=ftbid;Password=ftbid_lokal");
        connectionStringBuilder.Database = "postgres";
        _adminConnectionString = connectionStringBuilder.ConnectionString;
        await using (var connection = new NpgsqlConnection(_adminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }
        connectionStringBuilder.Database = _databaseName;
        ConnectionString = connectionStringBuilder.ConnectionString;
        _previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", ConnectionString);
        Factory = new TestApiFactory(new RsaSecurityKey(_rsa) { KeyId = "testnokkel" });
        _ = Factory.CreateClient();
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<FtbidDbContext>();
        await database.Database.MigrateAsync();
        database.Kommuner.AddRange(new Kommune { Kommunenummer = "0301" }, new Kommune { Kommunenummer = "5001" },
            new Kommune { Kommunenummer = "9998", GyldigTil = DateTimeOffset.UtcNow.AddDays(-1) });
        database.Skjemametadata.Add(new Skjemametadata { Arkivreferanse = "urn:dibk:bygg:innsending-id:123456789", Skjematype = "Søknad om rammetillatelse", Tiltakstyper = ["nyttanlegg"] });
        await database.SaveChangesAsync();
    }

    /// <summary>Fjerner kun databasen som dette testoppsettet selv opprettet.</summary>
    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _previousConnectionString);
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
        _rsa.Dispose();
    }

    /// <summary>Lager en HTTP-klient med et lokalt RSA-signert Maskinporten-lignende token.</summary>
    public HttpClient CreateClient(string scope = "dibk:bygg/ftbid.les dibk:bygg/ftbid.endre", string variant = "gyldig", string orgnummer = "123456789")
    {
        var client = Factory.CreateClient();
        if (variant == "mangler") return client;
        var claims = new List<Claim> { new("scope", scope) };
        if (variant != "uten_konsument") claims.Add(new("consumer", variant == "ugyldig_konsument" ? "[]" : $"{{\"ID\":\"0192:{orgnummer}\"}}"));
        var key = new RsaSecurityKey(_rsa) { KeyId = "testnokkel" };
        using var otherRsa = RSA.Create(2048);
        var signingCredentials = new SigningCredentials(variant == "feil_signatur" ? new RsaSecurityKey(otherRsa) { KeyId = "testnokkel" } : key, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(variant == "feil_utsteder" ? "https://feil.no/" : Issuer,
            claims: claims, notBefore: DateTime.UtcNow.AddHours(-1),
            expires: variant == "utlopt" ? DateTime.UtcNow.AddMinutes(-5) : DateTime.UtcNow.AddMinutes(5), signingCredentials: signingCredentials);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private sealed class TestApiFactory(RsaSecurityKey key) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(MaskinportenAuthentication.Scheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                configuration.SigningKeys.Add(key);
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            }));
        }
    }
}
