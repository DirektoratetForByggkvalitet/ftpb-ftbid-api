using Ftpb.Ftbid.Application.Matrikkel;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Infrastructure.Matrikkel;
using Ftpb.Ftbid.Infrastructure.Persistens;
using Ftpb.Ftbid.Tests.Oppsett;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Prosjekt = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Tests.Integrasjon;

[Collection("Postgres")]
public sealed class AdresseberikelseTester(PostgresApiOppsett oppsett)
{
    private FtbidDbContext Database() => new(new DbContextOptionsBuilder<FtbidDbContext>().UseNpgsql(oppsett.Tilkobling).Options);

    [Fact]
    public async Task FremdriftAdresserOgGenforsoegPersisteresPerEiendom()
    {
        await using var database = Database();
        var startId = await database.Set<Eiendom>().MaxAsync(e => (long?)e.Id) ?? 0;
        var prosjekt = new Prosjekt
        {
            Ean = "test" + Guid.NewGuid().ToString("N")[..14], Utstedt = DateTimeOffset.UtcNow,
            Eiendom = Enumerable.Range(0, 4).Select(i => new Eiendom
            {
                Rekkefolge = i, Eiendomsidentifikasjon = new("0301", i < 2 ? 1 : i, 2, 0, 7),
                Adresse = i == 0 ? [new() { Adresselinje1 = "Manuell", Rekkefolge = 0 }] : []
            }).ToList()
        };
        database.Add(prosjekt);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        var feil = true;
        var klient = new Oppslag((ident, _) =>
        {
            if (ident.Gaardsnummer == 2 && feil) throw new HttpRequestException("Midlertidig feil");
            IReadOnlyList<Eiendomsadresse> adresser = ident.Gaardsnummer == 3 ? [] :
                [new() { Adresselinje1 = "Adresse 1" }, new() { Adresselinje1 = "Adresse 2" }];
            return Task.FromResult(adresser);
        });
        var jobb = new Adresseberikelse(database, klient, NullLogger<Adresseberikelse>.Instance);
        Assert.NotNull(await jobb.BehandleBatchAsync(startId, 10, 0, () => true, default));
        var lagret = await database.Set<Eiendom>().AsNoTracking().Include(e => e.Adresse)
            .Where(e => e.Ftbid == prosjekt.Ean).OrderBy(e => e.Rekkefolge).ToArrayAsync();
        Assert.Equal([3, 2, 0, 0], lagret.Select(e => e.Adresse.Count));
        Assert.NotNull(lagret[0].MatrikkelAdresserHentet);
        Assert.NotNull(lagret[1].MatrikkelAdresserHentet);
        Assert.Null(lagret[2].MatrikkelAdresserHentet);
        Assert.NotNull(lagret[3].MatrikkelAdresserHentet); // Null treff er et fullført oppslag.
        Assert.Equal(3, klient.Antall); // Samme matrikkelnummer slås opp én gang per batch.

        // Ny DbContext simulerer omstart: bare feilet eiendom skal hentes igjen.
        feil = false;
        await using var nyDatabase = Database();
        await new Adresseberikelse(nyDatabase, klient, NullLogger<Adresseberikelse>.Instance)
            .BehandleBatchAsync(startId, 10, 0, () => true, default);
        Assert.Equal(4, klient.Antall);
        Assert.Equal(2, await nyDatabase.Set<Eiendomsadresse>().CountAsync(a => a.EiendomId == lagret[2].Id));
        Assert.Null(await jobb.BehandleBatchAsync(startId, 10, 0, () => true, default));
    }

    [Fact]
    public async Task DeaktiveringOgEndretMatrikkelnummerForhindrerFeilSkriving()
    {
        await using var database = Database();
        var prosjekt = new Prosjekt { Ean = "test" + Guid.NewGuid().ToString("N")[..14], Utstedt = DateTimeOffset.UtcNow,
            Eiendom = [new() { Eiendomsidentifikasjon = new("0301", 1, 2) }] };
        database.Add(prosjekt);
        await database.SaveChangesAsync();
        var id = prosjekt.Eiendom[0].Id;
        database.ChangeTracker.Clear();
        var aktivert = false;
        var klient = new Oppslag(async (_, ct) =>
        {
            await using var annenDatabase = Database();
            var eiendom = await annenDatabase.Set<Eiendom>().SingleAsync(e => e.Id == id, ct);
            eiendom.Eiendomsidentifikasjon = new("0301", 99, 2);
            await annenDatabase.SaveChangesAsync(ct);
            return [new() { Adresselinje1 = "Gammel eiendom" }];
        });
        var jobb = new Adresseberikelse(database, klient, NullLogger<Adresseberikelse>.Instance);
        await jobb.BehandleBatchAsync(id - 1, 1, 0, () => aktivert, default);
        Assert.Equal(0, klient.Antall);
        aktivert = true;
        await jobb.BehandleBatchAsync(id - 1, 1, 0, () => aktivert, default);
        var resultat = await database.Set<Eiendom>().AsNoTracking().Include(e => e.Adresse).SingleAsync(e => e.Id == id);
        Assert.Empty(resultat.Adresse);
        Assert.Null(resultat.MatrikkelAdresserHentet);
        Assert.Equal(99, resultat.Eiendomsidentifikasjon.Gaardsnummer);
    }

    private sealed class Oppslag(Func<Eiendomsidentifikasjon, CancellationToken, Task<IReadOnlyList<Eiendomsadresse>>> hent) : IMatrikkelAdresseoppslag
    {
        public int Antall { get; private set; }
        public Task<IReadOnlyList<Eiendomsadresse>> HentAsync(Eiendomsidentifikasjon identifikasjon, CancellationToken avbrudd)
        {
            Antall++;
            return hent(identifikasjon, avbrudd);
        }
    }
}
