using System.IO.Compression;
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
