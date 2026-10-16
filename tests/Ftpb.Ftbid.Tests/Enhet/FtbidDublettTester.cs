using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Domain.Prosjekter;
using Xunit;
using Prosjekt = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Tests.Enhet;

public sealed class FtbidDublettTester
{
    private readonly FtbidDublettService _service = new();
    private static Prosjekt Ny(string navn = "Tilbygg Solsiden skole", string kommune = "0301", int seksjon = 0) => new()
    {
        Ean = "709999900000000017", Prosjektnavn = navn, System = "Test",
        Eiendom = [new() { Eiendomsidentifikasjon = new(kommune, 1, 2, 0, seksjon) }]
    };

    [Fact]
    public void SammeMatrikkelOgNormalisertNavnErDublett()
    {
        var treff = _service.Finn(Ny("  TILBYGG   Solsiden skole "), [Ny()]);
        Assert.NotNull(treff);
        Assert.False(treff.Sannsynlig);
        Assert.Contains("Eiendomsidentifikasjon", treff.SamsvarendeFelter);
    }

    [Theory]
    [InlineData("sak")]
    [InlineData("referanse")]
    [InlineData("prosjektnummer")]
    public void UnikeReferanserGirDublettUtenSammeNavnEllerMatrikkel(string felt)
    {
        var ny = Ny(); var gammel = Ny("Annet prosjekt", seksjon: 1);
        if (felt == "sak") { ny.KommunaltSaksnummer = " SAK-1 "; gammel.KommunaltSaksnummer = "sak-1"; }
        if (felt == "referanse") { ny.EksternProsjektreferanse = "ref-1"; gammel.EksternProsjektreferanse = "REF-1"; }
        if (felt == "prosjektnummer") { ny.Prosjektnummer = "ref-1"; gammel.Prosjektnummer = "REF-1"; }
        Assert.False(_service.Finn(ny, [gammel])!.Sannsynlig);
        gammel.Eiendom[0].Eiendomsidentifikasjon = gammel.Eiendom[0].Eiendomsidentifikasjon with { Kommunenummer = "5001" };
        Assert.Null(_service.Finn(ny, [gammel]));
    }

    [Fact]
    public void EksternReferanseErAvgrensetTilSystem()
    {
        var ny = Ny(); var gammel = Ny("Annet prosjekt");
        ny.EksternProsjektreferanse = gammel.EksternProsjektreferanse = "1";
        gammel.System = "Annet";
        Assert.Null(_service.Finn(ny, [gammel]));
    }

    [Fact]
    public void SekundarMatrikkelOgFullstendigMatrikkelnummerBrukes()
    {
        var ny = Ny(); var gammel = Ny(seksjon: 1);
        Assert.Null(_service.Finn(ny, [gammel]));
        gammel.Eiendom.Add(ny.Eiendom[0]);
        Assert.False(_service.Finn(ny, [gammel])!.Sannsynlig);
    }

    [Fact]
    public void SkrivefeilMedStedstreffGirSannsynligDublett()
    {
        Assert.True(_service.Finn(Ny("Tilbygg Solsiden skolle"), [Ny()])!.Sannsynlig);
        Assert.Null(_service.Finn(Ny("Tilbygg skole byggetrinn 1"), [Ny("Tilbygg skole byggetrinn 2")]));
        Assert.Null(_service.Finn(Ny("Tilbygg Solsiden skolle", seksjon: 1), [Ny()]));
    }

    [Fact]
    public void AdresseOgNavnGirSannsynligDublett()
    {
        var ny = Ny(); var gammel = Ny(seksjon: 1);
        ny.Eiendom[0].Adresse = [new() { Adresselinje1 = "  Storgata  1 " }];
        gammel.Eiendom[0].Adresse = [new() { Adresselinje1 = "STORGATA 1" }];
        Assert.True(_service.Finn(ny, [gammel])!.Sannsynlig);
        gammel.Prosjektnavn = "Riving garasje";
        Assert.Null(_service.Finn(ny, [gammel]));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StedTiltakstypeOgTiltakshaverGirSannsynligDublett(bool organisasjon)
    {
        var ny = Ny(); var gammel = Ny("Annet navn");
        ny.Tiltakstype = gammel.Tiltakstype = "Tilbygg";
        if (organisasjon) ny.Organisasjonsnummer = gammel.Organisasjonsnummer = "123456789";
        else ny.Tiltakshaver = gammel.Tiltakshaver = "Ola Nordmann";
        Assert.True(_service.Finn(ny, [gammel])!.Sannsynlig);
        gammel.Tiltakstype = null;
        Assert.Null(_service.Finn(ny, [gammel]));
    }

    [Fact]
    public void BygningOgNavnGirSannsynligDublett()
    {
        var ny = Ny(); var gammel = Ny(seksjon: 1);
        ny.Eiendom[0].Bygningsnummer = gammel.Eiendom[0].Bygningsnummer = "12345";
        Assert.True(_service.Finn(ny, [gammel])!.Sannsynlig);
    }

    [Fact]
    public void EksaktTreffPrioriteresForanSannsynligTreff()
    {
        var sannsynlig = Ny("Tilbygg Solsiden skolle"); sannsynlig.Ean = "1";
        Assert.Equal(Ny().Ean, _service.Finn(Ny(), [sannsynlig, Ny()])!.Ftbid);
    }
}
