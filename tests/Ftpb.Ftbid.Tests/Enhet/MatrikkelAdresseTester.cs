using System.Xml.Serialization;
using Ftpb.Ftbid.Api.Bakgrunn;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Infrastructure.Matrikkel;
using Ftpb.Ftbid.Infrastructure.Matrikkel.Soap;
using Xunit;

namespace Ftpb.Ftbid.Tests.Enhet;

public sealed class MatrikkelAdresseTester
{
    [Fact]
    public async Task AdresserViaByggTasMedSelvOmEiendommenHarDirekteAdresse()
    {
        // Regresjon: 4602/203/17 har én direkte adresse, men to adresser via bygg.
        var resultat = await MatrikkelAdresseoppslag.HentAlleAdresseIdsAsync(
            () => Task.FromResult<AdresseId[]>([new VegadresseId { value = 67280564 }]),
            () => Task.FromResult<ByggId[]>([new BygningId { value = 67287114 }, new BygningId { value = 67287120 }]),
            _ => Task.FromResult<ByggIdTilAdresseIdsMapEntry[]>([
                new() { key = new BygningId { value = 67287114 }, value = [new VegadresseId { value = 67280564 }, new VegadresseId { value = 362424659 }] },
                new() { key = new BygningId { value = 67287120 }, value = [new VegadresseId { value = 67280564 }] }
            ]));
        Assert.Equal([67280564L, 362424659L], resultat.Select(a => a.value));
    }

    [Fact]
    public async Task TomDirekteAdresselisteHindrerIkkeOppslagViaByggOgTomBygningErGyldig()
    {
        var resultat = await MatrikkelAdresseoppslag.HentAlleAdresseIdsAsync(
            () => Task.FromResult<AdresseId[]>([]),
            () => Task.FromResult<ByggId[]>([new BygningId { value = 1 }, new BygningId { value = 2 }]),
            _ => Task.FromResult<ByggIdTilAdresseIdsMapEntry[]>([
                new() { key = new BygningId { value = 1 }, value = [] },
                new() { key = new BygningId { value = 2 }, value = [new VegadresseId { value = 3 }] }
            ]));
        Assert.Equal(3, Assert.Single(resultat).value);
    }

    [Fact]
    public async Task FeilEllerUfullstendigeBygningsoppslagGirIkkeEtDelvisVellykketAdresseoppslag()
    {
        Task<AdresseId[]> Direkte() => Task.FromResult<AdresseId[]>([new VegadresseId { value = 10 }]);
        Task<ByggId[]> Bygg() => Task.FromResult<ByggId[]>([new BygningId { value = 1 }]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => MatrikkelAdresseoppslag.HentAlleAdresseIdsAsync(
            Direkte, Bygg, _ => Task.FromResult<ByggIdTilAdresseIdsMapEntry[]>([])));
        await Assert.ThrowsAsync<HttpRequestException>(() => MatrikkelAdresseoppslag.HentAlleAdresseIdsAsync(
            Direkte, Bygg, _ => throw new HttpRequestException("Midlertidig feil")));
    }

    [Fact]
    public void JobbenErDeaktivertSomStandard() => Assert.False(new AdresseberikelseInnstillinger().Aktivert);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 7)]
    public void HeleMatrikkelnummeretSendesOgsaaMedNullverdier(int feste, int seksjon)
    {
        var ident = MatrikkelAdresseoppslag.LagIdent(new("0301", 12, 34, feste, seksjon));
        using var tekst = new StringWriter();
        new XmlSerializer(typeof(MatrikkelenhetIdent)).Serialize(tekst, ident);
        var xml = System.Xml.Linq.XDocument.Parse(tekst.ToString());
        string Verdi(string navn) => xml.Descendants().Single(e => e.Name.LocalName == navn).Value;
        Assert.Equal("0301", Verdi("kommunenummer"));
        Assert.Equal("12", Verdi("gardsnummer"));
        Assert.Equal("34", Verdi("bruksnummer"));
        Assert.Equal(feste.ToString(), Verdi("festenummer"));
        Assert.Equal(seksjon.ToString(), Verdi("seksjonsnummer"));
    }

    [Fact]
    public void VegadresseBevarerTilleggsnavnOgPostnummerMedLedendeNull()
    {
        var adresse = MatrikkelAdresseoppslag.TilAdresse(new Vegadresse
        {
            vegId = new VegId { value = 1 }, nummer = 2, nummerSpecified = true, bokstav = "A",
            adressetilleggsnavn = "Hovedinngang", kretsIds = [new PostnummeromradeId { value = 9 }]
        }, new("0301", 1, 2), new Dictionary<long, Veg> { [1] = new() { adressenavn = "Testveien" } },
            new Dictionary<long, Postnummeromrade> { [9] = new() { kretsnummer = 123, kretsnavn = "OSLO" } });
        Assert.Equal("Testveien 2A", adresse.Adresselinje1);
        Assert.Equal("Hovedinngang", adresse.Adresselinje2);
        Assert.Equal("Testveien", adresse.Gatenavn);
        Assert.Equal("2", adresse.Husnr);
        Assert.Equal("A", adresse.Bokstav);
        Assert.Equal("0123", adresse.Postnr);
        Assert.Equal("OSLO", adresse.Poststed);
        Assert.Equal("NO", adresse.Landkode);
    }

    [Fact]
    public void MatrikkeladresseUtenVegnavnBevares()
    {
        var adresse = MatrikkelAdresseoppslag.TilAdresse(new Matrikkeladresse
        {
            undernummer = 3, undernummerSpecified = true, adressetilleggsnavn = "Fjellhytta"
        }, new("0301", 12, 34, 2, 7), new Dictionary<long, Veg>(), new Dictionary<long, Postnummeromrade>());
        Assert.Equal("12/34/2-3", adresse.Adresselinje1);
        Assert.Equal("Fjellhytta", adresse.Adresselinje2);
        Assert.Null(adresse.Gatenavn);
        Assert.Null(adresse.Postnr);
    }

    [Fact]
    public void AlleManglendeAdresserLeggesTilUtenATappeEksisterendeEllerDeleEntiteter()
    {
        var eiendom = new Eiendom { Id = 1, Adresse = [new() { Adresselinje1 = " Eksisterende ", Rekkefolge = 5 }] };
        Eiendomsadresse[] treff = [new() { Adresselinje1 = "eksisterende" }, new() { Adresselinje1 = "Ny 1" },
            new() { Adresselinje1 = "Ny 2" }, new() { Adresselinje1 = "Ny 1" }];
        Assert.Equal(2, Adresseberikelse.LeggTilManglende(eiendom, treff));
        Assert.Equal(0, Adresseberikelse.LeggTilManglende(eiendom, treff));
        Assert.Equal(3, eiendom.Adresse.Count);
        Assert.Equal([5, 6, 7], eiendom.Adresse.Select(a => a.Rekkefolge));
        var annen = new Eiendom { Id = 2 };
        Adresseberikelse.LeggTilManglende(annen, treff);
        Assert.NotSame(eiendom.Adresse[1], annen.Adresse[1]);
        Assert.All(treff, a => Assert.Equal(0, a.EiendomId));
    }
}
