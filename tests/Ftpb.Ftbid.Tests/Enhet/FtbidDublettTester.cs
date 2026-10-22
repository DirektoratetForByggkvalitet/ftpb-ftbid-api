using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Domain.Prosjekter;
using Xunit;
using Prosjekt = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Tests.Enhet;

public sealed class FtbidDublettTester
{
    private readonly FtbidDublettService _service = new();

    [Fact]
    public void TiltakstyperSammenlignesSomEtNormalisertSett()
    {
        var ny = Ny(); var gammel = Ny();
        ny.Tiltakstyper = [" RIVING ", "Pa\u030abygg", "påbygg"];
        gammel.Tiltakstyper = ["Påbygg", "Riving"];
        Assert.Equal(TiltakstypeSett.LagHash(ny.Tiltakstyper), TiltakstypeSett.LagHash(gammel.Tiltakstyper));
        Assert.NotNull(_service.Finn(ny, [gammel]));
        gammel.Tiltakstyper = ["Riving"];
        Assert.Null(_service.Finn(ny, [gammel]));
        gammel.Tiltakstyper = ["Riving", "Påbygg", "Tilbygg"];
        Assert.Null(_service.Finn(ny, [gammel]));
    }

    [Fact]
    public void HashSkillerMellomVerdierOgSkilletegn()
    {
        Assert.NotEqual(TiltakstypeSett.LagHash(["a", "b"]), TiltakstypeSett.LagHash(["a,b"]));
        Assert.NotEqual(TiltakstypeSett.LagHash(["ab", "c"]), TiltakstypeSett.LagHash(["a", "bc"]));
        Assert.Equal(TiltakstypeSett.LagHash([" NYTT\t  ANLEGG "]), TiltakstypeSett.LagHash(["nytt anlegg"]));
    }
    private static Prosjekt Ny() => new()
    {
        Ean = "709999900000000017", Prosjektnavn = "Tilbygg Solsiden skole", System = "Test",
        Prosjektnummer = "P-1001", Tiltakstyper = ["Tilbygg"],
        Eiendom = [new() { Eiendomsidentifikasjon = new("0301", 1, 2) }]
    };

    [Fact]
    public void AlleFireOpplysningerGirDublettMedNormalisertTekst()
    {
        var ny = Ny();
        ny.Prosjektnavn = "  TILBYGG   Solsiden skole ";
        ny.Prosjektnummer = " p-1001 "; ny.Tiltakstyper = [" TILBYGG "];
        ny.System = "Annet system";
        var treff = _service.Finn(ny, [Ny()]);
        Assert.NotNull(treff);
        Assert.Equal(Ny().Ean, treff.Ftbid);
        Assert.Equal(["Prosjektnavn", "Prosjektnummer", "Tiltakstyper", "Matrikkelopplysninger"], treff.SamsvarendeFelter);
    }

    [Theory]
    [InlineData("navn")]
    [InlineData("nummer")]
    [InlineData("type")]
    [InlineData("kommune")]
    [InlineData("gard")]
    [InlineData("bruk")]
    public void EttAvvikTillatesSelvOmAlleAndreSignalerMatcher(string felt)
    {
        var ny = Ny(); var gammel = Ny();
        ny.KommunaltSaksnummer = gammel.KommunaltSaksnummer = "SAK-1";
        ny.EksternProsjektreferanse = gammel.EksternProsjektreferanse = "REF-1";
        ny.Organisasjonsnummer = gammel.Organisasjonsnummer = "123456789";
        ny.Tiltakshaver = gammel.Tiltakshaver = "Ola Nordmann";
        ny.Eiendom[0].Bygningsnummer = gammel.Eiendom[0].Bygningsnummer = "12345";
        ny.Eiendom[0].Adresse = gammel.Eiendom[0].Adresse = [new() { Adresselinje1 = "Storgata 1" }];
        if (felt == "navn") ny.Prosjektnavn += "e";
        if (felt == "nummer") ny.Prosjektnummer += "2";
        if (felt == "type") ny.Tiltakstyper = ["Riving"];
        var ident = ny.Eiendom[0].Eiendomsidentifikasjon;
        ny.Eiendom[0].Eiendomsidentifikasjon = felt switch {
            "kommune" => ident with { Kommunenummer = "5001" },
            "gard" => ident with { Gaardsnummer = 3 },
            "bruk" => ident with { Bruksnummer = 3 }, _ => ident };
        Assert.Null(_service.Finn(ny, [gammel]));
    }

    [Fact]
    public void FesteOgSeksjonInngaarIkkeOgSekundaereiendomKontrolleres()
    {
        var ny = Ny(); var gammel = Ny();
        gammel.Eiendom[0].Eiendomsidentifikasjon = new("5001", 3, 4);
        Assert.Null(_service.Finn(ny, [gammel]));
        gammel.Eiendom.Add(new() { Eiendomsidentifikasjon = new("0301", 1, 2, 3, 4) });
        Assert.NotNull(_service.Finn(ny, [gammel]));
    }

    [Fact]
    public void MatrikkelfelteneMaaTreffeSammeEiendom()
    {
        var gammel = Ny();
        gammel.Eiendom = [new() { Eiendomsidentifikasjon = new("0301", 1, 9) },
            new() { Eiendomsidentifikasjon = new("0301", 9, 2) }];
        Assert.Null(_service.Finn(Ny(), [gammel]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ManglendeEldreProsjektnummerEllerTiltakstypeGirIkkeDublett(string? verdi)
    {
        var gammel = Ny(); gammel.Prosjektnummer = verdi;
        Assert.Null(_service.Finn(Ny(), [gammel]));
        gammel = Ny(); gammel.Tiltakstyper = verdi is null ? [] : [verdi];
        Assert.Null(_service.Finn(Ny(), [gammel]));
    }
}
