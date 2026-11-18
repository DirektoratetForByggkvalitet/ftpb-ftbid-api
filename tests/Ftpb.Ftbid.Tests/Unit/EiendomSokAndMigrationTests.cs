using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Infrastructure.Persistence;
using Ftpb.Ftbid.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Ftpb.Ftbid.Tests.Unit;

/// <summary>Kontrollerer PostgreSQL-oversettelse og migrasjonsgenerering uten å åpne en database.</summary>
public sealed class EiendomSokAndMigrationTests
{
    [Fact]
    public void KombinerteSokefiltreKanOversettesTilPostgres()
    {
        using var database = new FtbidDbContext(new DbContextOptionsBuilder<FtbidDbContext>().UseNpgsql("Host=localhost;Database=sqltest").Options);
        var repository = new PostgresFtbidRepository(database, new FtbidDublettService());
        var sql = repository.BuildQuery(new FtbidSok(Kommunenummer: "0301", Gaardsnummer: 124, Bruksnummer: 55,
            Festenummer: 2, Seksjonsnummer: 0, Adresse: "Admiralveien", Bygningsnummer: "300827824", Bolignummer: "H0201",
            Tiltakstype: "Tilbygg", AnsvarligSokerOrganisasjonsnummer: "123456789", Prosjektnavn: "Skole")).ToQueryString();
        Assert.Contains("eiendom", sql);
        Assert.Contains("eiendomsadresse", sql);
        Assert.Contains("bolignummer", sql);
        Assert.Contains("bygningsnummer", sql);
        Assert.Contains("adresselinje1", sql);
    }

    [Fact]
    public void MigrationKanGenereresMedDatabevaringForGamleTabellerDroppes()
    {
        using var database = new FtbidDbContext(new DbContextOptionsBuilder<FtbidDbContext>().UseNpgsql("Host=localhost;Database=sqltest").Options);
        var migrator = database.GetService<IMigrator>();
        var sql = migrator.GenerateScript("20261016072024_FjernFtIdType");
        Assert.True(sql.IndexOf("CREATE TEMP TABLE gamle_eiendom_matrikler", StringComparison.Ordinal) <
            sql.IndexOf("DROP TABLE matrikkelopplysning", StringComparison.Ordinal));
        Assert.Contains("INSERT INTO eiendomsadresse", sql);
        Assert.Contains("a.adressetekst", sql);
        Assert.Contains("e.rekkefolge = 0", sql);
        Assert.False(database.Database.HasPendingModelChanges());
    }
}
