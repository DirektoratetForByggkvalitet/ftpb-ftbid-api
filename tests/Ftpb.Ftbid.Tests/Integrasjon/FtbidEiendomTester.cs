using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Infrastructure.Persistens;
using Ftpb.Ftbid.Tests.Oppsett;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ftpb.Ftbid.Tests.Integrasjon;

/// <summary>Verifiserer eiendomsendringer, hovedverdier, samtidighet og tilgang mot PostgreSQL.</summary>
[Collection("Postgres")]
public sealed class FtbidEiendomTester(PostgresApiOppsett oppsett)
{
    private static OpprettFtbid NyttProsjekt() => new()
    {
        Kommunenummer = "0301", Gaardsnummer = 10, Bruksnummer = 1,
        Prosjektnavn = "Eiendomstest " + Guid.NewGuid().ToString("N"), System = "Testsystem",
        Adresser = ["Hovedgata 1", "Andregata 2"], Matrikkelopplysninger = [new("5001", 20, 2, 3, 4)]
    };

    private static string Matrikler(string ftbid, string kommune = "0301", int gard = 10, int bruk = 1,
        int feste = 0, int seksjon = 0) => $"/api/ftbid/{ftbid}/matrikkelopplysninger?kommunenummer={kommune}" +
        $"&gaardsnummer={gard}&bruksnummer={bruk}&festenummer={feste}&seksjonsnummer={seksjon}";

    private static async Task<string> OpprettAsync(HttpClient klient, OpprettFtbid? prosjekt = null)
    {
        using var svar = await klient.PostAsJsonAsync("/api/ftbid", prosjekt ?? NyttProsjekt());
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        return (await svar.Content.ReadFromJsonAsync<string>())!;
    }

    private static async Task<FtbidResultat> HentAsync(HttpClient klient, string ftbid) =>
        Assert.Single((await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/" + ftbid))!);

    private static async Task KontrollerFeilAsync(HttpResponseMessage svar, string kode)
    {
        using (svar)
        {
            Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
            Assert.Equal("application/problem+json", svar.Content.Headers.ContentType?.MediaType);
            using var problem = JsonDocument.Parse(await svar.Content.ReadAsStringAsync());
            Assert.Equal(kode, problem.RootElement.GetProperty("ftbidFeil").GetProperty("Kode").GetString());
        }
    }

    /// <summary>Adresseendringer er idempotente og holder hovedadresse og søk konsistente.</summary>
    [Fact]
    public async Task AdresserKanLeggesTilOgFjernes()
    {
        using var klient = oppsett.Klient();
        var ftbid = await OpprettAsync(klient);
        var sti = $"/api/ftbid/{ftbid[^8..]}/adresser";
        foreach (var adresse in new[] { "  Tredjegata 3  ", "tredjegata 3" })
            Assert.Equal(HttpStatusCode.NoContent, (await klient.PostAsJsonAsync(sti, new AdresseEndring(adresse))).StatusCode);
        Assert.Equal(3, (await HentAsync(klient, ftbid)).Adresser.Count);
        foreach (var adresse in new[] { "HOVEDGATA 1", "Hovedgata 1", "finnes ikke" })
            Assert.Equal(HttpStatusCode.NoContent, (await klient.DeleteAsync(sti + "?adresse=" + Uri.EscapeDataString(adresse))).StatusCode);
        Assert.Equal("Andregata 2", (await HentAsync(klient, ftbid)).Adresse);
        using (var omfang = oppsett.Fabrikk.Services.CreateScope())
            Assert.Equal("Andregata 2", (await omfang.ServiceProvider.GetRequiredService<FtbidDbContext>().Ftbider.SingleAsync(f => f.Ean == ftbid)).Adresse);
        foreach (var adresse in new[] { "Andregata 2", "Tredjegata 3" })
            Assert.Equal(HttpStatusCode.NoContent, (await klient.DeleteAsync(sti + "?adresse=" + Uri.EscapeDataString(adresse))).StatusCode);
        var prosjekt = await HentAsync(klient, ftbid);
        Assert.Empty(prosjekt.Adresser);
        Assert.Null(prosjekt.Adresse);
        Assert.Equal(HttpStatusCode.NoContent, (await klient.PostAsJsonAsync(sti, new AdresseEndring("Ny hovedadresse"))).StatusCode);
        Assert.Equal("Ny hovedadresse", (await HentAsync(klient, ftbid)).Adresse);
        var sok = await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/sok?adresse=Ny%20hovedadresse");
        Assert.Contains(sok!, f => f.Ftbid == ftbid);
    }

    /// <summary>Matrikkelendringer bruker eksakt identitet og oppdaterer hovedfeltene og opprettingsidentiteten.</summary>
    [Fact]
    public async Task MatriklerKanLeggesTilOgFjernes()
    {
        using var klient = oppsett.Klient();
        var opprinnelig = NyttProsjekt();
        var ftbid = await OpprettAsync(klient, opprinnelig);
        var sti = $"/api/ftbid/{ftbid}/matrikkelopplysninger";
        var tillegg = new Matrikkeldata(" 0301 ", 30, 3, 5, 6);
        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.NoContent, (await klient.PostAsJsonAsync(sti, tillegg)).StatusCode);
        Assert.Equal(3, (await HentAsync(klient, ftbid)).Matrikkelopplysninger.Count);
        Assert.Equal(HttpStatusCode.NoContent, (await klient.DeleteAsync(Matrikler(ftbid, "0301", 30, 3, 5, 0))).StatusCode);
        Assert.Equal(3, (await HentAsync(klient, ftbid)).Matrikkelopplysninger.Count);
        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.NoContent, (await klient.DeleteAsync(Matrikler(ftbid[^8..]))).StatusCode);
        var prosjekt = await HentAsync(klient, ftbid);
        Assert.Equal("5001", prosjekt.Kommunenummer);
        Assert.Equal(20, prosjekt.Gaardsnummer);
        Assert.Equal(3, prosjekt.Festenummer);
        using (var omfang = oppsett.Fabrikk.Services.CreateScope())
        {
            var lagret = await omfang.ServiceProvider.GetRequiredService<FtbidDbContext>().Ftbider.SingleAsync(f => f.Ean == ftbid);
            Assert.Equal("5001", lagret.Kommunenummer);
            Assert.Equal(20, lagret.Gaardsnummer);
        }
        var gjenbruk = new OpprettFtbid { Kommunenummer = "5001", Gaardsnummer = 20, Bruksnummer = 2,
            Festenummer = 3, Seksjonsnummer = 4, Prosjektnavn = opprinnelig.Prosjektnavn, System = "Testsystem" };
        var dublett = await klient.PostAsJsonAsync("/api/ftbid", gjenbruk);
        Assert.Equal(HttpStatusCode.Conflict, dublett.StatusCode);
        Assert.Equal("/api/ftbid/" + ftbid, dublett.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.NoContent, (await klient.DeleteAsync(Matrikler(ftbid, "0301", 30, 3, 5, 6))).StatusCode);
        await KontrollerFeilAsync(await klient.DeleteAsync(Matrikler(ftbid, "5001", 20, 2, 3, 4)), "V0061");
        Assert.Single((await HentAsync(klient, ftbid)).Matrikkelopplysninger);
    }

    /// <summary>En kollisjon ved bytte av hovedmatrikkel ruller tilbake hele endringen.</summary>
    [Fact]
    public async Task HovedmatrikkelKanIkkeGiDuplisertProsjektidentitet()
    {
        using var klient = oppsett.Klient();
        var prosjekt = NyttProsjekt();
        prosjekt.Matrikkelopplysninger = [];
        var ftbid = await OpprettAsync(klient, prosjekt);
        await OpprettAsync(klient, new OpprettFtbid { Kommunenummer = "5001", Gaardsnummer = 20, Bruksnummer = 2,
            Festenummer = 3, Seksjonsnummer = 4, Prosjektnavn = prosjekt.Prosjektnavn, System = "Testsystem" });
        Assert.Equal(HttpStatusCode.NoContent, (await klient.PostAsJsonAsync($"/api/ftbid/{ftbid}/matrikkelopplysninger",
            new Matrikkeldata("5001", 20, 2, 3, 4))).StatusCode);
        await KontrollerFeilAsync(await klient.DeleteAsync(Matrikler(ftbid)), "V0062");
        var uendret = await HentAsync(klient, ftbid);
        Assert.Equal("0301", uendret.Kommunenummer);
        Assert.Equal(2, uendret.Matrikkelopplysninger.Count);
    }

    /// <summary>Endring krever en eksisterende ftbid og gyldige verdier med stabile regelkoder.</summary>
    [Fact]
    public async Task UgyldigeEndringerAvvises()
    {
        using var klient = oppsett.Klient();
        var ftbid = await OpprettAsync(klient);
        var sti = $"/api/ftbid/{ftbid}";
        await KontrollerFeilAsync(await klient.PostAsJsonAsync(sti + "/adresser", new AdresseEndring(" ")), "V0042");
        await KontrollerFeilAsync(await klient.PostAsJsonAsync(sti + "/adresser", new AdresseEndring(new string('x', 1001))), "V0047");
        await KontrollerFeilAsync(await klient.PostAsJsonAsync(sti + "/matrikkelopplysninger", new Matrikkeldata("9999", 1, 1)), "V0034");
        await KontrollerFeilAsync(await klient.PostAsJsonAsync(sti + "/matrikkelopplysninger", new Matrikkeldata("0301", -1, 1)), "V0049");
        await KontrollerFeilAsync(await klient.DeleteAsync(sti + "/matrikkelopplysninger?kommunenummer=0301"), "V0042");
        await KontrollerFeilAsync(await klient.PostAsJsonAsync("/api/ftbid/99999999/adresser", new AdresseEndring("Ny adresse")), "V0054");
        await KontrollerFeilAsync(await klient.DeleteAsync("/api/ftbid/99999999/adresser?adresse=Ny"), "V0054");
        await KontrollerFeilAsync(await klient.PostAsJsonAsync("/api/ftbid/99999999/matrikkelopplysninger", new Matrikkeldata("0301", 1, 1)), "V0054");
        await KontrollerFeilAsync(await klient.DeleteAsync(Matrikler("99999999")), "V0054");
    }

    /// <summary>Historiske kommuner kan fjernes uten å kreve dagens kommunegyldighet.</summary>
    [Fact]
    public async Task HistoriskMatrikkelKanFjernes()
    {
        using var klient = oppsett.Klient();
        var ftbid = await OpprettAsync(klient);
        using (var omfang = oppsett.Fabrikk.Services.CreateScope())
        {
            var database = omfang.ServiceProvider.GetRequiredService<FtbidDbContext>();
            var prosjekt = await database.Ftbider.Include(f => f.Matrikkelopplysninger).SingleAsync(f => f.Ean == ftbid);
            prosjekt.Matrikkelopplysninger.Add(new() { Ftbid = ftbid, Rekkefolge = 2, Kommunenummer = "9998", Gaardsnummer = 40, Bruksnummer = 4 });
            await database.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NoContent, (await klient.DeleteAsync(Matrikler(ftbid, "9998", 40, 4))).StatusCode);
        Assert.Equal(2, (await HentAsync(klient, ftbid)).Matrikkelopplysninger.Count);
    }

    /// <summary>Listegrensene gjelder også ved endring, mens eksisterende verdier kan legges til på nytt.</summary>
    [Fact]
    public async Task ListegrenserHaandhevesVedTillegg()
    {
        using var klient = oppsett.Klient();
        var prosjekt = NyttProsjekt();
        prosjekt.Adresser = Enumerable.Range(0, 100).Select(i => $"Testadresse {i}").ToList();
        prosjekt.Matrikkelopplysninger = Enumerable.Range(20, 99).Select(i => new Matrikkeldata("0301", i, 1)).ToList();
        var ftbid = await OpprettAsync(klient, prosjekt);
        var sti = $"/api/ftbid/{ftbid}";
        Assert.Equal(HttpStatusCode.NoContent, (await klient.PostAsJsonAsync(sti + "/adresser", new AdresseEndring("Testadresse 0"))).StatusCode);
        await KontrollerFeilAsync(await klient.PostAsJsonAsync(sti + "/adresser", new AdresseEndring("For mye")), "V0048");
        Assert.Equal(HttpStatusCode.NoContent, (await klient.PostAsJsonAsync(sti + "/matrikkelopplysninger", new Matrikkeldata("0301", 10, 1))).StatusCode);
        await KontrollerFeilAsync(await klient.PostAsJsonAsync(sti + "/matrikkelopplysninger", new Matrikkeldata("0301", 900, 1)), "V0048");
    }

    /// <summary>Samtidige tillegg mister ingen adresser og oppretter ikke duplikater.</summary>
    [Fact]
    public async Task SamtidigeTilleggErAtomiske()
    {
        using var klient = oppsett.Klient();
        var ftbid = await OpprettAsync(klient);
        var verdier = Enumerable.Repeat("Lik adresse", 8).Concat(Enumerable.Range(0, 8).Select(i => $"Samtidig adresse {i}"));
        var svar = await Task.WhenAll(verdier.Select(a => klient.PostAsJsonAsync($"/api/ftbid/{ftbid}/adresser", new AdresseEndring(a))));
        Assert.All(svar, s => Assert.Equal(HttpStatusCode.NoContent, s.StatusCode));
        Assert.Equal(11, (await HentAsync(klient, ftbid)).Adresser.Count);
        var slettinger = await Task.WhenAll(klient.DeleteAsync(Matrikler(ftbid)), klient.DeleteAsync(Matrikler(ftbid, "5001", 20, 2, 3, 4)));
        Assert.Single(slettinger, s => s.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(slettinger, s => s.StatusCode == HttpStatusCode.BadRequest);
        Assert.Single((await HentAsync(klient, ftbid)).Matrikkelopplysninger);
    }

    /// <summary>De fire endringsoperasjonene avviser manglende token og manglende endrescope.</summary>
    [Theory]
    [InlineData("mangler", HttpStatusCode.Unauthorized)]
    [InlineData("gyldig", HttpStatusCode.Forbidden)]
    public async Task EiendomsendringerKreverEndretilgang(string variant, HttpStatusCode forventet)
    {
        using var oppretter = oppsett.Klient();
        var ftbid = await OpprettAsync(oppretter);
        using var klient = oppsett.Klient(scope: "dibk:bygg/ftbid.les", variant: variant);
        var sti = $"/api/ftbid/{ftbid}";
        Assert.Equal(forventet, (await klient.PostAsJsonAsync(sti + "/adresser", new AdresseEndring("Ny"))).StatusCode);
        Assert.Equal(forventet, (await klient.DeleteAsync(sti + "/adresser?adresse=Hovedgata%201")).StatusCode);
        Assert.Equal(forventet, (await klient.PostAsJsonAsync(sti + "/matrikkelopplysninger", new Matrikkeldata("0301", 30, 3))).StatusCode);
        Assert.Equal(forventet, (await klient.DeleteAsync(Matrikler(ftbid))).StatusCode);
        Assert.Equal(2, (await HentAsync(oppretter, ftbid)).Matrikkelopplysninger.Count);
    }
}
