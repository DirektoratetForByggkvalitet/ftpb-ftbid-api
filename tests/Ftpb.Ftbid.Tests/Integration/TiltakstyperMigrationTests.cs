using Ftpb.Ftbid.Infrastructure.Persistence;
using Ftpb.Ftbid.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Ftpb.Ftbid.Tests.Integration;

[Collection("Postgres")]
public sealed class TiltakstyperMigrationTests(PostgresApiFixture fixture)
{
    [Fact]
    public async Task OppgraderingBevarerEnkeltverdierOgNedgraderingAvviserDatatap()
    {
        var name = "ftbid_migrasjon_" + Guid.NewGuid().ToString("N");
        await using var administrasjon = new NpgsqlConnection(fixture.ConnectionString);
        await administrasjon.OpenAsync();
        await using (var opprett = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", administrasjon))
            await opprett.ExecuteNonQueryAsync();
        try
        {
            var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = name };
            await using var database = new FtbidDbContext(new DbContextOptionsBuilder<FtbidDbContext>().UseNpgsql(connectionString.ConnectionString).Options);
            var migrator = database.GetService<IMigrator>();
            const string forrige = "20261016073751_InnforEiendomsmodell";
            await migrator.MigrateAsync(forrige);
            await database.Database.ExecuteSqlRawAsync("""
                INSERT INTO ftbid (ean, prosjektnavn, normalisert_prosjektnavn, system, utstedt, tiltakstype)
                VALUES ('1', 'Eldre prosjekt', 'eldre prosjekt', 'Test', now(), 'Påbygg'),
                       ('2', 'Uten type', 'uten type', 'Test', now(), NULL);
                """);
            await migrator.MigrateAsync();
            var prosjekter = await database.Ftbider.AsNoTracking().OrderBy(f => f.Ean).ToListAsync();
            Assert.Equal(["Påbygg"], prosjekter[0].Tiltakstyper);
            Assert.Null(prosjekter[0].TiltakstyperHash);
            Assert.Empty(prosjekter[1].Tiltakstyper);
            await migrator.MigrateAsync(forrige);
            var gammelType = await database.Database.SqlQueryRaw<string>("SELECT tiltakstype AS \"Value\" FROM ftbid WHERE ean = '1'").SingleAsync();
            Assert.Equal("Påbygg", gammelType);
            await migrator.MigrateAsync();
            await database.Database.ExecuteSqlRawAsync("UPDATE ftbid SET tiltakstyper = ARRAY['Påbygg', 'Riving'] WHERE ean = '1'");
            var error = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(forrige));
            Assert.Contains("flere tiltakstyper", error.MessageText);
        }
        finally
        {
            await using var fjern = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", administrasjon);
            await fjern.ExecuteNonQueryAsync();
        }
    }
}
