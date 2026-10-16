using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Tests.Oppsett;
using Xunit;

namespace Ftpb.Ftbid.Tests.Integrasjon;

/// <summary>Verifiserer utvidede søkefiltre mot API-et og ekte PostgreSQL.</summary>
[Collection("Postgres")]
public sealed class UtvidetSokTester(PostgresApiOppsett oppsett)
{
    private static OpprettFtbid NyttProsjekt() => new()
    {
        Prosjektnavn = "Søketest " + Guid.NewGuid().ToString("N"), System = "Søketester",
        Eiendom = [new() { Eiendomsidentifikasjon = new("0301", 71, 9), Adresse = [new("Søkeadresse 1")] },
            new() { Eiendomsidentifikasjon = new("5001", 82, 10, 3, 4),
                Adresse = [new("Søkeadresse " + Guid.NewGuid().ToString("N"))],
                Bygningsnummer = "BYGG-" + Guid.NewGuid(), Bolignummer = "H0101" }],
        EksternProsjektreferanse = "REF-" + Guid.NewGuid(), KommunaltSaksnummer = "SAK-" + Guid.NewGuid(),
        Tiltakstype = "Tiltak-" + Guid.NewGuid(),
        AnsvarligSokerOrganisasjonsnummer = "987654321",
        Organisasjonsnummer = "123456789"
    };

    [Fact]
    public async Task NyeFelterLagresOgHvertKriteriumKanBrukesAlene()
    {
        using var klient = oppsett.Klient();
        var prosjekt = NyttProsjekt();
        var opprettet = await klient.PostAsJsonAsync("/api/ftbid", prosjekt);
        Assert.Equal(HttpStatusCode.OK, opprettet.StatusCode);
        var ftbid = (await opprettet.Content.ReadFromJsonAsync<string>())!;
        var hentet = Assert.Single((await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/" + ftbid))!);
        Assert.Equal(prosjekt.Eiendom[1].Bolignummer, hentet.Eiendom[1].Bolignummer);
        Assert.Equal(prosjekt.AnsvarligSokerOrganisasjonsnummer, hentet.AnsvarligSokerOrganisasjonsnummer);
        Assert.Equal(prosjekt.Organisasjonsnummer, hentet.Organisasjonsnummer);
        var filtre = new Dictionary<string, string>
        {
            ["ftid"] = ftbid, ["prosjektnavn"] = prosjekt.Prosjektnavn!,
            ["tiltaksnavn"] = prosjekt.Prosjektnavn!, ["tiltakstype"] = prosjekt.Tiltakstype!,
            ["eksternprosjektreferanse"] = prosjekt.EksternProsjektreferanse!,
            ["kommunaltsaksnummer"] = prosjekt.KommunaltSaksnummer!,
            ["ansvarligsokerorganisasjonsnummer"] = prosjekt.AnsvarligSokerOrganisasjonsnummer!,
            ["bygningsnummer"] = prosjekt.Eiendom[1].Bygningsnummer!, ["bolignummer"] = "h0101",
            ["adresse"] = prosjekt.Eiendom[1].Adresse[0].Adresselinje1!, ["kommunenummer"] = "5001",
            ["gaardsnummer"] = "82", ["bruksnummer"] = "10", ["festenummer"] = "3", ["seksjonsnummer"] = "4"
        };
        foreach (var (felt, verdi) in filtre)
        {
            var resultater = await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/sok?" + felt + "=" +
                Uri.EscapeDataString(" " + verdi.ToUpperInvariant() + " "));
            Assert.Contains(resultater!, f => f.Ftbid == ftbid);
        }
        var kort = await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/sok?ftid=" + ftbid[^8..]);
        Assert.Equal(ftbid, Assert.Single(kort!).Ftbid);
        var alleFiltre = string.Join('&', filtre.Select(f => f.Key + "=" + Uri.EscapeDataString(f.Value)));
        Assert.Equal(ftbid, Assert.Single((await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/sok?" + alleFiltre))!).Ftbid);
        Assert.Empty((await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/sok?ftid=" + ftbid +
            "&ansvarligsokerorganisasjonsnummer=" + prosjekt.Organisasjonsnummer))!);
    }

    [Fact]
    public async Task EksakteFiltreDelviseNavnOgMatrikkelKombineresRiktig()
    {
        using var klient = oppsett.Klient();
        var prosjekt = NyttProsjekt();
        var svar = await klient.PostAsJsonAsync("/api/ftbid", prosjekt);
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var ftbid = (await svar.Content.ReadFromJsonAsync<string>())!;
        var grunnlag = "/api/ftbid/sok?ftid=" + ftbid;
        foreach (var feilfilter in new[] { "festenummer=0&seksjonsnummer=4", "kommunenummer=0301&gaardsnummer=82",
            "gaardsnummer=71&bruksnummer=9&festenummer=3", "gaardsnummer=82&seksjonsnummer=0",
            "tiltakstype=ukjent", "bygningsnummer=ukjent", "bolignummer=ukjent",
            "kommunaltsaksnummer=ukjent", "eksternprosjektreferanse=" + Uri.EscapeDataString(prosjekt.EksternProsjektreferanse![..8]) })
            Assert.Empty((await klient.GetFromJsonAsync<List<FtbidResultat>>(grunnlag + "&" + feilfilter))!);
        Assert.Equal(ftbid, Assert.Single((await klient.GetFromJsonAsync<List<FtbidResultat>>(grunnlag +
            "&prosjektnavn=SØKETEST&tiltaksnavn=SØKE&adresse=SØKEADRESSE&festenummer=0&seksjonsnummer=0"))!).Ftbid);
    }

    [Fact]
    public async Task EksternReferanseBrukerProsjektnummerSomReserve()
    {
        using var klient = oppsett.Klient();
        var prosjekt = NyttProsjekt();
        prosjekt.EksternProsjektreferanse = null;
        prosjekt.Prosjektnr = "RESERVE-" + Guid.NewGuid();
        var svar = await klient.PostAsJsonAsync("/api/ftbid", prosjekt);
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var ftbid = await svar.Content.ReadFromJsonAsync<string>();
        var resultater = await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/sok?eksternprosjektreferanse=" +
            Uri.EscapeDataString(prosjekt.Prosjektnr.ToLowerInvariant()));
        Assert.Equal(ftbid, Assert.Single(resultater!).Ftbid);
    }

    [Fact]
    public async Task TiltakstypeKanTreffeTilknyttetSkjema()
    {
        using var klient = oppsett.Klient();
        var svar = await klient.PostAsJsonAsync("/api/ftbid", NyttProsjekt());
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var ftbid = await svar.Content.ReadFromJsonAsync<string>();
        Assert.Equal(HttpStatusCode.NoContent, (await klient.PostAsJsonAsync("/api/ftbid/tilknytt",
            new { Ftbid = ftbid, Arkivreferanse = "AR123456789" })).StatusCode);
        var resultater = await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/sok?ftid=" + ftbid + "&tiltakstype=NYTTANLEGG");
        Assert.Equal(ftbid, Assert.Single(resultater!).Ftbid);
    }

    [Theory]
    [InlineData("festenummer=-1")]
    [InlineData("seksjonsnummer=-1")]
    [InlineData("ftid=ugyldig")]
    [InlineData("kommunenummer=abc")]
    [InlineData("ansvarligsokerorganisasjonsnummer=123")]
    [InlineData("ansvarligsokerorganisasjonsnummer=12345678a")]
    [InlineData("ftid=%20&tiltakstype=%20&bolignummer=%20")]
    public async Task UgyldigeSokGir400(string filter)
    {
        using var klient = oppsett.Klient();
        var svar = await klient.GetAsync("/api/ftbid/sok?" + filter);
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
        Assert.Equal("application/problem+json", svar.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("organisasjonsnummer")]
    [InlineData("bolignummer")]
    public async Task UgyldigeNyeOpprettingsfelterAvvises(string felt)
    {
        using var klient = oppsett.Klient();
        var prosjekt = NyttProsjekt();
        if (felt == "organisasjonsnummer") prosjekt.AnsvarligSokerOrganisasjonsnummer = "12345678x";
        if (felt == "bolignummer") prosjekt.Eiendom[1].Bolignummer = new string('H', 51);
        Assert.Equal(HttpStatusCode.BadRequest, (await klient.PostAsJsonAsync("/api/ftbid", prosjekt)).StatusCode);
    }

    [Fact]
    public async Task OpenApiBeskriverAlleSokefiltre()
    {
        using var klient = oppsett.Klient();
        using var dokument = JsonDocument.Parse(await klient.GetStringAsync("/openapi/v1.json"));
        var parametere = dokument.RootElement.GetProperty("paths").GetProperty("/api/ftbid/sok")
            .GetProperty("get").GetProperty("parameters").EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToList();
        foreach (var felt in new[] { "ftid", "prosjektnavn", "tiltaksnavn", "tiltakstype", "eksternprosjektreferanse",
            "kommunaltsaksnummer", "ansvarligsokerorganisasjonsnummer", "bygningsnummer", "bolignummer", "adresse",
            "kommunenummer", "gaardsnummer", "bruksnummer", "festenummer", "seksjonsnummer" })
            Assert.Contains(felt, parametere);
    }
}
