using Ftpb.Ftbid.Application.Konfigurasjon;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Lagring;
using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using FtbidEntitet = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;
using Xunit;

namespace Ftpb.Ftbid.Tests.Enhet;

/// <summary>Kontrollerer matrikkeldata uten eksterne eiendomsoppslag eller database.</summary>
public sealed class FtbidMatrikkelTester
{
    private const string Ftbid = "709999900000000017";

    [Fact]
    public async Task OpprettingBevarerAlleMatrikkelfelterUtenEksterntOppslag()
    {
        var lager = new TestLager();
        var service = new FtbidOpprettingService(lager, new Kommuneregister(), new FtbidInnstillinger());
        var sekundar = new Matrikkeldata(" 0301 ", 9_999_999, 888_888, 33, 44);
        var prosjekt = new OpprettFtbid { Prosjektnavn = "Test", System = "Test", Kommunenummer = "0301",
            Gaardsnummer = 100, Bruksnummer = 200, Festenummer = 3, Seksjonsnummer = 4,
            Matrikkelopplysninger = [sekundar] };

        Assert.Equal(Ftbid, await service.OpprettAsync(prosjekt));
        Assert.NotNull(lager.Opprettet);
        Assert.Equal(2, lager.Opprettet.Matrikkelopplysninger.Count);
        var lagret = lager.Opprettet.Matrikkelopplysninger[1];
        Assert.Equal("0301", lagret.Kommunenummer);
        Assert.Equal(sekundar.Gaardsnummer, lagret.Gaardsnummer);
        Assert.Equal(sekundar.Bruksnummer, lagret.Bruksnummer);
        Assert.Equal(sekundar.Festenummer, lagret.Festenummer);
        Assert.Equal(sekundar.Seksjonsnummer, lagret.Seksjonsnummer);
    }

    [Fact]
    public async Task TilleggAvMatrikkelKreverIkkeTreffIEtEksterntRegister()
    {
        var lager = new TestLager();
        var service = new FtbidEiendomService(lager, new Kommuneregister(), new FtbidInnstillinger());
        var matrikkel = new Matrikkeldata(" 0301 ", 9_999_999, 888_888, 33, 44);

        await service.LeggTilMatrikkelAsync(Ftbid, matrikkel);

        Assert.Equal(Ftbid, lager.EndretFtbid);
        Assert.Equal(matrikkel with { Kommunenummer = "0301" }, lager.Tilfoyd);
    }

    [Fact]
    public async Task OpprettingKreverFortsattMinstEnMatrikkel()
    {
        var lager = new TestLager();
        var service = new FtbidOpprettingService(lager, new Kommuneregister(), new FtbidInnstillinger());
        await Assert.ThrowsAsync<PåkrevdVerdiManglerException>(() => service.OpprettAsync(
            new OpprettFtbid { Prosjektnavn = "Uten matrikkel", System = "Test", Matrikkelopplysninger = [] }));
        Assert.Null(lager.Opprettet);
    }

    [Theory]
    [InlineData(-1, 1, 0, 0)]
    [InlineData(1, -1, 0, 0)]
    [InlineData(1, 1, -1, 0)]
    [InlineData(1, 1, 0, -1)]
    public async Task NegativeMatrikkelnummerAvvisesLokalt(int gard, int bruk, int feste, int seksjon)
    {
        var lager = new TestLager();
        var service = new FtbidEiendomService(lager, new Kommuneregister(), new FtbidInnstillinger());
        await Assert.ThrowsAsync<UgyldigeMatrikkelopplysningerException>(() =>
            service.LeggTilMatrikkelAsync(Ftbid, new("0301", gard, bruk, feste, seksjon)));
        Assert.Null(lager.Tilfoyd);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("9999")]
    public async Task NyeMatriklerKreverFortsattGyldigKommune(string kommune)
    {
        var lager = new TestLager();
        var service = new FtbidEiendomService(lager, new Kommuneregister(), new FtbidInnstillinger());
        await Assert.ThrowsAsync<UgyldigKommunenummerException>(() =>
            service.LeggTilMatrikkelAsync(Ftbid, new(kommune, 1, 1)));
        Assert.Null(lager.Tilfoyd);
    }

    private sealed class Kommuneregister : IKommuneregister
    {
        public Task<bool> ErKommuneGyldigAsync(string kommunenummer, DateTimeOffset tidspunkt, CancellationToken avbrudd) =>
            Task.FromResult(kommunenummer == "0301");
    }

    private sealed class TestLager : IFtbidLager, IFtbidEiendomslager
    {
        public FtbidEntitet? Opprettet { get; private set; }
        public Matrikkeldata? Tilfoyd { get; private set; }
        public string? EndretFtbid { get; private set; }

        public Task<string> OpprettAsync(FtbidEntitet prosjekt, string gs1Prefiks, CancellationToken avbrudd)
        {
            Opprettet = prosjekt;
            return Task.FromResult(FtbidMatrikkelTester.Ftbid);
        }

        public Task LeggTilMatrikkelAsync(string ftbid, Matrikkeldata matrikkel, CancellationToken avbrudd)
        {
            EndretFtbid = ftbid;
            Tilfoyd = matrikkel;
            return Task.CompletedTask;
        }

        public Task TilknyttAsync(string ftbid, string arkivreferanse, CancellationToken avbrudd) => throw new NotSupportedException();
        public Task<IReadOnlyList<FtbidEntitet>> HentAsync(string ftbid, CancellationToken avbrudd) => throw new NotSupportedException();
        public Task<IReadOnlyList<FtbidEntitet>> SokAsync(FtbidSok filter, CancellationToken avbrudd) => throw new NotSupportedException();
        public Task LeggTilAdresseAsync(string ftbid, string adresse, CancellationToken avbrudd) => throw new NotSupportedException();
        public Task FjernAdresseAsync(string ftbid, string adresse, CancellationToken avbrudd) => throw new NotSupportedException();
        public Task FjernMatrikkelAsync(string ftbid, Matrikkeldata matrikkel, CancellationToken avbrudd) => throw new NotSupportedException();
    }
}
