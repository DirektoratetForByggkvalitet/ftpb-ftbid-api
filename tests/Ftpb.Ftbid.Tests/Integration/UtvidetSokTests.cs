using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Tests.Fixtures;
using Xunit;

namespace Ftpb.Ftbid.Tests.Integration;

/// <summary>Verifiserer utvidede søkefiltre mot API-et og ekte PostgreSQL.</summary>
[Collection("Postgres")]
public sealed class UtvidetSokTests(PostgresApiFixture fixture)
{
    private static OpprettFtbid NyttProsjekt() => new()
    {
        Prosjektnavn = "Søketest " + Guid.NewGuid().ToString("N"), System = "Søketester", Prosjektnr = "P-1001",
        Eiendom = [new() { Eiendomsidentifikasjon = new("0301", 71, 9), Adresse = [new("Søkeadresse 1")] },
            new() { Eiendomsidentifikasjon = new("5001", 82, 10, 3, 4),
                Adresse = [new("Søkeadresse " + Guid.NewGuid().ToString("N"))],
                Bygningsnummer = "BYGG-" + Guid.NewGuid(), Bolignummer = "H0101" }],
        EksternProsjektreferanse = "REF-" + Guid.NewGuid(), KommunaltSaksnummer = "SAK-" + Guid.NewGuid(),
        Tiltakstyper = ["Tiltak-" + Guid.NewGuid()],
        AnsvarligSokerOrganisasjonsnummer = "987654321",
        Organisasjonsnummer = "123456789"
    };

    [Fact]
    public async Task NyeFelterLagresOgHvertKriteriumKanBrukesAlene()
    {
        using var client = fixture.CreateClient();
        var prosjekt = NyttProsjekt();
        var opprettet = await client.PostAsJsonAsync("/api/ftbid", prosjekt);
        Assert.Equal(HttpStatusCode.OK, opprettet.StatusCode);
        var ftbid = (await opprettet.Content.ReadFromJsonAsync<string>())!;
        var hentet = Assert.Single((await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid/" + ftbid))!);
        Assert.Equal(prosjekt.Eiendom[1].Bolignummer, hentet.Eiendom[1].Bolignummer);
        Assert.Equal(prosjekt.AnsvarligSokerOrganisasjonsnummer, hentet.AnsvarligSokerOrganisasjonsnummer);
        Assert.Equal(prosjekt.Organisasjonsnummer, hentet.Organisasjonsnummer);
        var filters = new Dictionary<string, string>
        {
            ["ftid"] = ftbid, ["prosjektnavn"] = prosjekt.Prosjektnavn!,
            ["tiltaksnavn"] = prosjekt.Prosjektnavn!, ["tiltakstype"] = prosjekt.Tiltakstyper[0],
            ["eksternprosjektreferanse"] = prosjekt.EksternProsjektreferanse!,
            ["kommunaltsaksnummer"] = prosjekt.KommunaltSaksnummer!,
            ["ansvarligsokerorganisasjonsnummer"] = prosjekt.AnsvarligSokerOrganisasjonsnummer!,
            ["bygningsnummer"] = prosjekt.Eiendom[1].Bygningsnummer!, ["bolignummer"] = "h0101",
            ["adresse"] = prosjekt.Eiendom[1].Adresse[0].Adresselinje1!, ["kommunenummer"] = "5001",
            ["gaardsnummer"] = "82", ["bruksnummer"] = "10", ["festenummer"] = "3", ["seksjonsnummer"] = "4"
        };
        foreach (var (field, value) in filters)
        {
            var results = await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid?" + field + "=" +
                Uri.EscapeDataString(" " + value.ToUpperInvariant() + " "));
            Assert.Contains(results!, f => f.Ftbid == ftbid);
        }
        var kort = await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid?ftid=" + ftbid[^8..]);
        Assert.Equal(ftbid, Assert.Single(kort!).Ftbid);
        var alleFiltre = string.Join('&', filters.Select(f => f.Key + "=" + Uri.EscapeDataString(f.Value)));
        Assert.Equal(ftbid, Assert.Single((await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid?" + alleFiltre))!).Ftbid);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/ftbid?ftid=" + ftbid +
            "&ansvarligsokerorganisasjonsnummer=" + prosjekt.Organisasjonsnummer)).StatusCode);
    }

    [Fact]
    public async Task EksakteFiltreDelviseNavnOgMatrikkelKombineresRiktig()
    {
        using var client = fixture.CreateClient();
        var prosjekt = NyttProsjekt();
        var response = await client.PostAsJsonAsync("/api/ftbid", prosjekt);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ftbid = (await response.Content.ReadFromJsonAsync<string>())!;
        var connectionStringBuilder = "/api/ftbid?ftid=" + ftbid;
        foreach (var feilfilter in new[] { "festenummer=0&seksjonsnummer=4", "kommunenummer=0301&gaardsnummer=82",
            "gaardsnummer=71&bruksnummer=9&festenummer=3", "gaardsnummer=82&seksjonsnummer=0",
            "tiltakstype=ukjent", "bygningsnummer=ukjent", "bolignummer=ukjent",
            "kommunaltsaksnummer=ukjent", "eksternprosjektreferanse=" + Uri.EscapeDataString(prosjekt.EksternProsjektreferanse![..8]) })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(connectionStringBuilder + "&" + feilfilter)).StatusCode);
        Assert.Equal(ftbid, Assert.Single((await client.GetFromJsonAsync<List<FtbidResult>>(connectionStringBuilder +
            "&prosjektnavn=SØKETEST&tiltaksnavn=SØKE&adresse=SØKEADRESSE&festenummer=0&seksjonsnummer=0"))!).Ftbid);
    }

    [Fact]
    public async Task EksternReferanseBrukerProsjektnummerSomReserve()
    {
        using var client = fixture.CreateClient();
        var prosjekt = NyttProsjekt();
        prosjekt.EksternProsjektreferanse = null;
        prosjekt.Prosjektnr = "RESERVE-" + Guid.NewGuid();
        var response = await client.PostAsJsonAsync("/api/ftbid", prosjekt);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ftbid = await response.Content.ReadFromJsonAsync<string>();
        var results = await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid?eksternprosjektreferanse=" +
            Uri.EscapeDataString(prosjekt.Prosjektnr.ToLowerInvariant()));
        Assert.Equal(ftbid, Assert.Single(results!).Ftbid);
    }

    [Fact]
    public async Task TiltakstypeKanTreffeTilknyttetSkjema()
    {
        using var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/ftbid", NyttProsjekt());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ftbid = await response.Content.ReadFromJsonAsync<string>();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/ftbid/{ftbid}/tilknytninger",
            new { Tilknytninger = new[] { new { ReferanseId = "urn:dibk:bygg:innsending-id:123456789", Typebeskrivelse = "Søknad om rammetillatelse", Tiltakstyper = new[] { "nyttanlegg" } } } })).StatusCode);
        var results = await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid?ftid=" + ftbid + "&tiltakstype=NYTTANLEGG");
        Assert.Equal(ftbid, Assert.Single(results!).Ftbid);
    }

    [Theory]
    [InlineData("festenummer=-1")]
    [InlineData("seksjonsnummer=-1")]
    [InlineData("ftid=ugyldig")]
    [InlineData("kommunenummer=abc")]
    [InlineData("ansvarligsokerorganisasjonsnummer=123")]
    [InlineData("ansvarligsokerorganisasjonsnummer=12345678a")]
    public async Task UgyldigeSokGir400(string filter)
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/ftbid?" + filter);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("organisasjonsnummer")]
    [InlineData("bolignummer")]
    public async Task UgyldigeNyeOpprettingsfelterAvvises(string field)
    {
        using var client = fixture.CreateClient();
        var prosjekt = NyttProsjekt();
        if (field == "organisasjonsnummer") prosjekt.AnsvarligSokerOrganisasjonsnummer = "12345678x";
        if (field == "bolignummer") prosjekt.Eiendom[1].Bolignummer = new string('H', 51);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/ftbid", prosjekt)).StatusCode);
    }

    [Fact]
    public async Task OpenApiBeskriverAlleSokefiltre()
    {
        using var client = fixture.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var parametere = document.RootElement.GetProperty("paths").GetProperty("/api/ftbid")
            .GetProperty("get").GetProperty("parameters").EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToList();
        foreach (var field in new[] { "ftid", "prosjektnavn", "tiltaksnavn", "tiltakstype", "eksternprosjektreferanse",
            "kommunaltsaksnummer", "ansvarligsokerorganisasjonsnummer", "bygningsnummer", "bolignummer", "adresse",
            "kommunenummer", "gaardsnummer", "bruksnummer", "festenummer", "seksjonsnummer" })
            Assert.Contains(field, parametere);
    }
}
