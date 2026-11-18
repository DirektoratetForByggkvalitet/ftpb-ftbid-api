using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using Ftpb.Ftbid.Application.Matrikkel;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Api.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Ftpb.Ftbid.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Ftpb.Ftbid.Tests.Integration;

/// <summary>Verifiserer snapshot, gjentatt deploy og avbrudd mot en isolert PostgreSQL-database.</summary>
public sealed class DevdataImportTests : IAsyncLifetime
{
    private readonly string _navn = "ftbid_test_devimport_" + Guid.NewGuid().ToString("N");
    private string _adminConnectionString = "";
    private FtbidDbContext _database = null!;

    public async Task InitializeAsync()
    {
        var options = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FTBID_TEST_POSTGRES") ??
            "Host=localhost;Port=55432;Database=postgres;Username=ftbid;Password=ftbid_lokal");
        options.Database = "postgres";
        _adminConnectionString = options.ConnectionString;
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_navn}\"", connection);
        await command.ExecuteNonQueryAsync();
        options.Database = _navn;
        _database = new FtbidDbContext(new DbContextOptionsBuilder<FtbidDbContext>().UseNpgsql(options.ConnectionString).Options);
        await _database.Database.MigrateAsync();
    }

    [Fact]
    public async Task TestOppstartImportererSnapshotOgBeholderFullTokenvalidering()
    {
        var oppslagStartet = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("ConnectionStrings:Postgres", _database.Database.GetConnectionString());
            builder.UseSetting("ElasticApm:Enabled", "false");
            builder.UseSetting("Serilog:Enabled", "false");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _database.Database.GetConnectionString(),
                ["Database:BrukEntraId"] = "false",
                ["Database:ImporterDevdata"] = "true",
                ["Adresseberikelse:Aktivert"] = "true",
                ["Matrikkel:Brukernavn"] = "test",
                ["Matrikkel:Passord"] = "test",
                ["ElasticApm:Enabled"] = "false",
                ["Serilog:Enabled"] = "false"
            }));
            builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(MaskinportenAuthentication.Scheme,
                options => options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                    new OpenIdConnectConfiguration { Issuer = "https://test.maskinporten.no/" })));
            builder.ConfigureServices(services =>
            {
                // Serviceregistreringen leser forbindelsen før ConfigureAppConfiguration
                // i minimal hosting. Overstyr også DbContext for å isolere denne verten.
                services.RemoveAll<DbContextOptions<FtbidDbContext>>();
                services.AddDbContext<FtbidDbContext>(options => options.UseNpgsql(_database.Database.GetConnectionString()));
                services.RemoveAll<IMatrikkelAdresseoppslag>();
                services.AddScoped<IMatrikkelAdresseoppslag>(provider =>
                    new TestAdresseoppslag(provider.GetRequiredService<FtbidDbContext>(), oppslagStartet));
            });
        });
        using var client = factory.CreateClient();
        Assert.Equal(13131, await _database.Ftbider.CountAsync());
        Assert.Equal(30944, await _database.Identifikasjonsmetadata.CountAsync());
        Assert.Equal(13131, await oppslagStartet.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (!await _database.Set<Eiendomsadresse>().AnyAsync(a => a.Adresselinje1 == "Adresse fra testoppslag", timeout.Token))
                await Task.Delay(50, timeout.Token);
        }
        var scheme = await factory.Services.GetRequiredService<IAuthenticationSchemeProvider>()
            .GetSchemeAsync(MaskinportenAuthentication.Scheme);
        Assert.Equal(typeof(JwtBearerHandler), scheme!.HandlerType);
        using var utenToken = await client.GetAsync("/api/ftbid/707338700000000014");
        Assert.Equal(HttpStatusCode.Unauthorized, utenToken.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "lokalt-token");
        using var ugyldigToken = await client.GetAsync("/api/ftbid/707338700000000014");
        Assert.Equal(HttpStatusCode.Unauthorized, ugyldigToken.StatusCode);
    }

    private sealed class TestAdresseoppslag(FtbidDbContext database, TaskCompletionSource<int> startet) : IMatrikkelAdresseoppslag
    {
        public async Task<IReadOnlyList<Eiendomsadresse>> GetAsync(Eiendomsidentifikasjon identifikasjon, CancellationToken cancellationToken)
        {
            startet.TrySetResult(await database.Ftbider.CountAsync(cancellationToken));
            return [new Eiendomsadresse { Adresselinje1 = "Adresse fra testoppslag" }];
        }
    }

    [Fact]
    public async Task SnapshotBevarerDataOgSekvenserOgImporteresBareEnGang()
    {
        var rot = new DirectoryInfo(AppContext.BaseDirectory);
        while (rot is not null && !File.Exists(Path.Combine(rot.FullName, "ftpb-ftbid-api.sln"))) rot = rot.Parent;
        var fil = Path.Combine(rot!.FullName, "data", "dev-seed", "lokal.sql.gz");
        Assert.True(await DevdataImport.ImportAsync(_database, fil));
        Assert.Equal(13131, await _database.Ftbider.CountAsync());
        Assert.Equal(30944, await _database.Identifikasjonsmetadata.CountAsync());
        Assert.Equal(13191, await _database.Database.SqlQueryRaw<long>("SELECT count(*) AS \"Value\" FROM public.eiendom").SingleAsync());
        Assert.Equal(60, await _database.Database.SqlQueryRaw<long>("SELECT count(*) AS \"Value\" FROM public.eiendomsadresse").SingleAsync());
        Assert.Equal(13133, await _database.Database.SqlQueryRaw<long>("SELECT nextval('public.ftbid_sekvens') AS \"Value\"").SingleAsync());
        Assert.Equal(13192, await _database.Database.SqlQueryRaw<long>("SELECT nextval('public.eiendom_id_seq') AS \"Value\"").SingleAsync());
        Assert.Equal(61, await _database.Database.SqlQueryRaw<long>("SELECT nextval('public.eiendomsadresse_id_seq') AS \"Value\"").SingleAsync());
        Assert.False(await DevdataImport.ImportAsync(_database, fil));
        Assert.Equal(13131, await _database.Ftbider.CountAsync());
        Assert.Equal(13134, await _database.Database.SqlQueryRaw<long>("SELECT nextval('public.ftbid_sekvens') AS \"Value\"").SingleAsync());
    }

    [Fact]
    public async Task EksisterendeDataBevaresOgFeiletImportRullesTilbake()
    {
        var fil = Path.GetTempFileName();
        try
        {
            await using (var ut = File.Create(fil))
            await using (var gzip = new GZipStream(ut, CompressionMode.Compress))
            await using (var skriver = new StreamWriter(gzip))
                await skriver.WriteAsync("INSERT INTO public.kommune (kommunenummer) VALUES ('9999'); SELECT 1/0;");
            await Assert.ThrowsAsync<PostgresException>(() => DevdataImport.ImportAsync(_database, fil));
            Assert.Empty(await _database.Kommuner.ToListAsync());
            await _database.Database.ExecuteSqlRawAsync("INSERT INTO public.kommune (kommunenummer) VALUES ('0301')");
            var error = await Assert.ThrowsAsync<PostgresException>(() => DevdataImport.ImportAsync(_database, fil));
            Assert.Contains("krever tomme datatabeller", error.MessageText);
            Assert.Equal("0301", (await _database.Kommuner.SingleAsync()).Kommunenummer);
        }
        finally { File.Delete(fil); }
    }

    public async Task DisposeAsync()
    {
        if (_database is not null) await _database.DisposeAsync();
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_navn}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}
