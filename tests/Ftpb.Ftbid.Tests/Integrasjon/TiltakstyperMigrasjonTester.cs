using Ftpb.Ftbid.Infrastructure.Persistens;
using Ftpb.Ftbid.Tests.Oppsett;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Ftpb.Ftbid.Tests.Integrasjon;

[Collection("Postgres")]
public sealed class TiltakstyperMigrasjonTester(PostgresApiOppsett oppsett)
{
    [Fact]
    public async Task OppgraderingBevarerEnkeltverdierOgNedgraderingAvviserDatatap()
    {
        var navn = "ftbid_migrasjon_" + Guid.NewGuid().ToString("N");
        await using var administrasjon = new NpgsqlConnection(oppsett.Tilkobling);
        await administrasjon.OpenAsync();
        await using (var opprett = new NpgsqlCommand($"CREATE DATABASE \"{navn}\"", administrasjon))
            await opprett.ExecuteNonQueryAsync();
        try
        {
            var tilkobling = new NpgsqlConnectionStringBuilder(oppsett.Tilkobling) { Database = navn };
            await using var database = new FtbidDbContext(new DbContextOptionsBuilder<FtbidDbContext>().UseNpgsql(tilkobling.ConnectionString).Options);
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
            var feil = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(forrige));
            Assert.Contains("flere tiltakstyper", feil.MessageText);
        }
        finally
        {
            await using var fjern = new NpgsqlCommand($"DROP DATABASE \"{navn}\" WITH (FORCE)", administrasjon);
            await fjern.ExecuteNonQueryAsync();
        }
    }
}
