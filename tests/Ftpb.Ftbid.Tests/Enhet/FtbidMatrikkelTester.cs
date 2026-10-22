using System.Text.Json;
using Ftpb.Ftbid.Application.Konfigurasjon;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Lagring;
using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Infrastructure.Persistens;
using Microsoft.EntityFrameworkCore;
using FtbidEntitet = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;
using Xunit;

namespace Ftpb.Ftbid.Tests.Enhet;

/// <summary>Tester den strukturerte eiendomsmodellen uten Docker, nettverk eller database.</summary>
public sealed class FtbidMatrikkelTester
{
    private const string Ftbid = "709999900000000017";
    private static Eiendomsdata Eiendom() => new() { Eiendomsidentifikasjon = new(" 0301 ", 124, 55, 2),
        Adresse = [new(" Admiral veien 2A ", "Hyttefelt 1", Postnr: "0318", Poststed: "Oslo", Landkode: "no",
            Gatenavn: "Admiralveien", Husnr: "02", Bokstav: "A")],
        Bygningsnummer = "300827824", Bolignummer = "H0201", Kommunenavn = "Oslo" };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EiendomKanIkkeUtelatesEllerVaereTom(bool eksplisitt)
    {
        var lager = new TestLager();
        var prosjekt = new OpprettFtbid { Prosjektnavn = "Uten eiendom", System = "Test", Prosjektnr = "P-1", Tiltakstyper = ["Tilbygg"] };
        if (eksplisitt) prosjekt.Eiendom = [];
        await Assert.ThrowsAsync<PåkrevdVerdiManglerException>(() =>
            new FtbidOpprettingService(lager, new Kommuneregister(), new()).OpprettAsync(prosjekt));
        Assert.Null(lager.Opprettet);
    }

    [Fact]
    public async Task FlereEiendommerOgStrukturerteAdresserBevaresVedOpprettingOgOppslag()
    {
        var lager = new TestLager();
        var andre = Eiendom();
        andre.Eiendomsidentifikasjon = new("0301", 9_999_999, 88, 3, 4);
        andre.Adresse = [new("Annen adresse"), new("Adresse nummer to")];
        andre.Bygningsnummer = "000123";
        var prosjekt = new OpprettFtbid { Prosjektnavn = "Test", System = "Test", Prosjektnr = "P-1001", Tiltakstyper = ["Tilbygg"], Eiendom = [Eiendom(), andre] };
        await new FtbidOpprettingService(lager, new Kommuneregister(), new()).OpprettAsync(prosjekt);
        lager.Opprettet!.Ean = Ftbid;
        var resultat = Assert.Single(await new FtbidOppslagService(lager, new()).HentAsync(Ftbid));
        Assert.Equal(2, resultat.Eiendom.Count);
        Assert.Equal(new Eiendomsidentifikasjon("0301", 124, 55, 2), resultat.Eiendom[0].Eiendomsidentifikasjon);
        Assert.Equal("0318", resultat.Eiendom[0].Adresse[0].Postnr);
        Assert.Equal("02", resultat.Eiendom[0].Adresse[0].Husnr);
        Assert.Equal("NO", resultat.Eiendom[0].Adresse[0].Landkode);
        Assert.Equal("Admiral veien 2A", resultat.Eiendom[0].Adresse[0].Adresselinje1);
        Assert.Equal("H0201", resultat.Eiendom[0].Bolignummer);
        Assert.Equal("Oslo", resultat.Eiendom[0].Kommunenavn);
        Assert.Equal("000123", resultat.Eiendom[1].Bygningsnummer);
        Assert.Equal(2, resultat.Eiendom[1].Adresse.Count);
        var json = JsonSerializer.Serialize(resultat);
        Assert.DoesNotContain("\"Matrikkelopplysninger\"", json);
        Assert.DoesNotContain("\"Adresser\"", json);
        Assert.Equal("0318", JsonSerializer.Deserialize<FtbidResultat>(json)!.Eiendom[0].Adresse[0].Postnr);
    }

    [Fact]
    public void GammelFlatKontraktAvvisesForAaUnngaaTapAvEiendomsdata()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OpprettFtbid>(
            """{ "Prosjektnavn": "Test", "System": "Test", "Adresser": ["Skal ikke forsvinne"] }"""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Eiendomsdata>(
            """{ "Matrikkelopplysninger": [{ "Kommunenummer": "0301" }] }"""));
    }

    [Fact]
    public async Task EiendomKanHaTomAdresseliste()
    {
        var lager = new TestLager(); var data = Eiendom(); data.Adresse = [];
        await new FtbidEiendomService(lager, new Kommuneregister(), new()).LeggTilAsync(Ftbid, data);
        Assert.Empty(lager.Tilfoyd!.Adresse);
    }

    [Fact]
    public async Task ErstatningOgSlettingBevarerFtbidOgEiendomsId()
    {
        var lager = new TestLager(); var service = new FtbidEiendomService(lager, new Kommuneregister(), new());
        await service.ErstattAsync(Ftbid[^8..], 42, Eiendom());
        Assert.Equal(Ftbid, lager.EndretFtbid);
        Assert.Equal(42, lager.EiendomId);
        await service.FjernAsync(Ftbid, 42);
        Assert.True(lager.Fjernet);
    }

    [Theory]
    [InlineData("identifikasjon")]
    [InlineData("adresse-null")]
    [InlineData("adresse-tom")]
    [InlineData("adresse-null-element")]
    [InlineData("postnr-for-langt")]
    [InlineData("for-mange-adresser")]
    public async Task UgyldigEiendomAvvisesForLagring(string variant)
    {
        var lager = new TestLager(); var data = Eiendom();
        if (variant == "identifikasjon") data.Eiendomsidentifikasjon = null;
        if (variant == "adresse-null") data.Adresse = null!;
        if (variant == "adresse-tom") data.Adresse = [new()];
        if (variant == "adresse-null-element") data.Adresse = [null!];
        if (variant == "postnr-for-langt") data.Adresse = [new(Postnr: new string('0', 21))];
        if (variant == "for-mange-adresser") data.Adresse = Enumerable.Repeat(new Adressedata("A"), 101).ToList();
        await Assert.ThrowsAnyAsync<ValideringsException>(() => new FtbidEiendomService(lager, new Kommuneregister(), new()).LeggTilAsync(Ftbid, data));
        Assert.Null(lager.Tilfoyd);
    }

    [Fact]
    public async Task UgyldigSekundaereiendomAvviserHeleOpprettingen()
    {
        var lager = new TestLager(); var andre = Eiendom(); andre.Eiendomsidentifikasjon = new("0301", -1, 1);
        await Assert.ThrowsAsync<UgyldigeMatrikkelopplysningerException>(() => new FtbidOpprettingService(lager, new Kommuneregister(), new())
            .OpprettAsync(new() { Prosjektnavn = "Test", System = "Test", Prosjektnr = "P-1001", Tiltakstyper = ["Tilbygg"], Eiendom = [Eiendom(), andre] }));
        Assert.Null(lager.Opprettet);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("9999")]
    public async Task NyeEiendommerKreverGyldigKommune(string kommune)
    {
        var lager = new TestLager(); var data = Eiendom(); data.Eiendomsidentifikasjon = new(kommune, 1, 1);
        await Assert.ThrowsAsync<UgyldigKommunenummerException>(() => new FtbidEiendomService(lager, new Kommuneregister(), new()).LeggTilAsync(Ftbid, data));
    }

    [Fact]
    public void EfModellenErGyldigOgMatcherMigrasjonene()
    {
        using var database = new FtbidDbContext(new DbContextOptionsBuilder<FtbidDbContext>().UseNpgsql("Host=localhost;Database=modelltest").Options);
        Assert.NotNull(database.Model.FindEntityType(typeof(Ftpb.Ftbid.Domain.Prosjekter.Eiendom)));
        Assert.False(database.Database.HasPendingModelChanges());
    }

    private sealed class Kommuneregister : IKommuneregister
    {
        public Task<bool> ErKommuneGyldigAsync(string kommunenummer, DateTimeOffset tidspunkt, CancellationToken avbrudd) => Task.FromResult(kommunenummer == "0301");
    }
    private sealed class TestLager : IFtbidLager, IFtbidEiendomslager
    {
        public FtbidEntitet? Opprettet { get; private set; }
        public Ftpb.Ftbid.Domain.Prosjekter.Eiendom? Tilfoyd { get; private set; }
        public string? EndretFtbid { get; private set; }
        public long EiendomId { get; private set; }
        public bool Fjernet { get; private set; }
        public Task<string> OpprettAsync(FtbidEntitet prosjekt, string gs1Prefiks, CancellationToken avbrudd) { Opprettet = prosjekt; return Task.FromResult(Ftbid); }
        public Task LeggTilAsync(string ftbid, Ftpb.Ftbid.Domain.Prosjekter.Eiendom eiendom, CancellationToken avbrudd) { EndretFtbid = ftbid; Tilfoyd = eiendom; return Task.CompletedTask; }
        public Task ErstattAsync(string ftbid, long eiendomId, Ftpb.Ftbid.Domain.Prosjekter.Eiendom eiendom, CancellationToken avbrudd) { EiendomId = eiendomId; return LeggTilAsync(ftbid, eiendom, avbrudd); }
        public Task FjernAsync(string ftbid, long eiendomId, CancellationToken avbrudd) { EndretFtbid = ftbid; EiendomId = eiendomId; Fjernet = true; return Task.CompletedTask; }
        public Task<IReadOnlyList<FtbidEntitet>> HentAsync(string ftbid, CancellationToken avbrudd) => Task.FromResult<IReadOnlyList<FtbidEntitet>>([Opprettet!]);
        public Task TilknyttAsync(string ftbid, string arkivreferanse, CancellationToken avbrudd) => throw new NotSupportedException();
        public Task<IReadOnlyList<FtbidEntitet>> SokAsync(FtbidSok filter, CancellationToken avbrudd) => throw new NotSupportedException();
    }
}
