using Ftpb.Ftbid.Tests.Fixtures;
using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Domain.Referansedata;
using Ftpb.Ftbid.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Ftpb.Ftbid.Tests.Integration;

/// <summary>Verifiserer API-kontrakt, PostgreSQL-lagring, Maskinporten og helsesjekker.</summary>
[Collection("Postgres")]
public sealed class FtbidApiTests(PostgresApiFixture fixture)
{
    private static OpprettFtbid NyttProsjekt(string? name = null) => new()
    {
        Prosjektnavn = name ?? "Prosjekt " + Guid.NewGuid().ToString("N"), System = "Testsystem", Prosjektnr = "P-1001", Tiltakstyper = ["Tilbygg"],
        Eiendom = [new() { Eiendomsidentifikasjon = new("0301", 10, 1),
            Adresse = [new("Karl Johans gate 1"), new("Storgata 2")] },
            new() { Eiendomsidentifikasjon = new("5001", 20, 2, 3, 4) }]
    };

    /// <summary>Returnerer eiendommer med identifikasjon og strukturerte adresser.</summary>
    [Fact]
    public async Task OpprettingOgOppslagBevarerModellen()
    {
        using var client = fixture.CreateClient();
        var request = NyttProsjekt();
        var response = await client.PostAsJsonAsync("/api/ftbid", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ftbid = (await response.Content.ReadFromJsonAsync<string>())!;
        Assert.Equal(18, ftbid.Length);
        Assert.StartsWith("7099999000", ftbid);
        var results = await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid/" + ftbid);
        var result = Assert.Single(results!);
        using var scope = fixture.Factory.Services.CreateScope();
        var utstedt = await scope.ServiceProvider.GetRequiredService<FtbidDbContext>().Ftbider
            .Where(f => f.Ean == ftbid).Select(f => f.Utstedt).SingleAsync();
        Assert.NotEqual(default, utstedt);
        Assert.Equal(utstedt, result.Utstedt);
        Assert.Equal(request.Prosjektnavn, result.Prosjektnavn);
        Assert.Equal("0301", result.Eiendom[0].Eiendomsidentifikasjon.Kommunenummer);
        Assert.Equal(2, result.Eiendom[0].Adresse.Count);
        Assert.Equal(2, result.Eiendom.Count);
        var kort = await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid/" + ftbid[^8..]);
        var kortResultat = Assert.Single(kort!);
        Assert.Equal(ftbid, kortResultat.Ftbid);
        Assert.Equal(utstedt, kortResultat.Utstedt);
        var json = await client.GetStringAsync("/api/ftbid/" + ftbid);
        Assert.Contains("\"Ftbid\"", json);
        Assert.Contains("\"Tilknytninger\"", json);
        foreach (var url in new[] { "/api/ftbid/" + ftbid, "/api/ftbid?ftid=" + ftbid })
        {
            using var document = JsonDocument.Parse(await client.GetStringAsync(url));
            var prosjekt = document.RootElement[0];
            Assert.Equal(utstedt, prosjekt.GetProperty("Utstedt").GetDateTimeOffset());
            Assert.Equal(JsonValueKind.Null, prosjekt.GetProperty("KommunaltSaksnummer").ValueKind);
            Assert.Empty(prosjekt.GetProperty("Tilknytninger").EnumerateArray());
            var eiendom = prosjekt.GetProperty("Eiendom");
            Assert.Equal(JsonValueKind.Null, eiendom[0].GetProperty("Bygningsnummer").ValueKind);
            Assert.Equal(JsonValueKind.Null, eiendom[0].GetProperty("Adresse")[0].GetProperty("Postnr").ValueKind);
            Assert.Empty(eiendom[1].GetProperty("Adresse").EnumerateArray());
        }
    }

    /// <summary>Støtter oppretting med kun de nye listene.</summary>
    [Fact]
    public async Task OpprettingMedKunListerFungerer()
    {
        using var client = fixture.CreateClient();
        var request = new OpprettFtbid { Prosjektnavn = Guid.NewGuid().ToString(), System = "Test", Prosjektnr = "P-1001", Tiltakstyper = ["Tilbygg"],
            Eiendom = [new() { Eiendomsidentifikasjon = new("0301", 1, 2), Adresse = [new("Adresse 1"), new("Adresse 2")] }, new() { Eiendomsidentifikasjon = new("5001", 3, 4) }] };
        var response = await client.PostAsJsonAsync("/api/ftbid", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ftbid = await response.Content.ReadFromJsonAsync<string>();
        var result = Assert.Single((await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid/" + ftbid))!);
        Assert.Equal("Adresse 1", result.Eiendom[0].Adresse[0].Adresselinje1);
        Assert.Equal(2, result.Eiendom.Count);
    }

    /// <summary>Kun én samtidig oppretting lykkes; dubletter returnerer 409 uten å endre prosjektet.</summary>
    [Fact]
    public async Task SamtidigOpprettingGirEnFtbidOgDublettkonflikter()
    {
        using var client = fixture.CreateClient();
        var request = NyttProsjekt();
        var response = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => client.PostAsJsonAsync("/api/ftbid", request)));
        var opprettet = Assert.Single(response, s => s.StatusCode == HttpStatusCode.OK);
        var ftbid = await opprettet.Content.ReadFromJsonAsync<string>();
        foreach (var dublett in response.Where(s => s != opprettet))
            await AssertDublettAsync(dublett, ftbid!, "V0067");
        request.Prosjektnavn = "  " + request.Prosjektnavn!.ToUpperInvariant() + "  ";
        request.Prosjektnr = " p-1001 ";
        await AssertDublettAsync(await client.PostAsJsonAsync("/api/ftbid", request), ftbid!, "V0067");
        var result = Assert.Single((await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid/" + ftbid))!);
        Assert.Equal("P-1001", result.Prosjektnummer);
        using var scope = fixture.Factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<FtbidDbContext>().Ftbider
            .CountAsync(f => f.Ean == ftbid));
    }

    [Fact]
    public async Task NyeOpplysningerLagresOgAnnetNavnTillates()
    {
        using var client = fixture.CreateClient();
        var request = NyttProsjekt();
        request.EksternProsjektreferanse = Guid.NewGuid().ToString();
        request.KommunaltSaksnummer = Guid.NewGuid().ToString();
        request.Tiltakshaver = "Ola Nordmann";
        request.Organisasjonsnummer = "123456789";
        request.Eiendom[0].Bygningsnummer = Guid.NewGuid().ToString();
        request.Tiltakstyper = ["Tilbygg"];
        var response = await client.PostAsJsonAsync("/api/ftbid", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ftbid = (await response.Content.ReadFromJsonAsync<string>())!;
        var result = Assert.Single((await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid/" + ftbid))!);
        Assert.Equal(request.EksternProsjektreferanse, result.EksternProsjektreferanse);
        Assert.Equal(request.KommunaltSaksnummer, result.KommunaltSaksnummer);
        Assert.Equal(request.Tiltakshaver, result.Tiltakshaver);
        Assert.Equal(request.Organisasjonsnummer, result.Organisasjonsnummer);
        Assert.Equal(request.Eiendom[0].Bygningsnummer, result.Eiendom[0].Bygningsnummer);
        Assert.Equal(request.Tiltakstyper, result.Tiltakstyper);
        request.EksternProsjektreferanse = null;
        request.KommunaltSaksnummer = null;
        request.Prosjektnavn = "Et helt annet navn";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/ftbid", request)).StatusCode);
    }

    [Theory]
    [InlineData("sak")]
    [InlineData("referanse")]
    public async Task ReferanserAleneAvviserIkkeOppretting(string field)
    {
        using var client = fixture.CreateClient();
        var request = NyttProsjekt();
        if (field == "sak") request.KommunaltSaksnummer = Guid.NewGuid().ToString();
        else request.EksternProsjektreferanse = Guid.NewGuid().ToString();
        var opprettet = await client.PostAsJsonAsync("/api/ftbid", request);
        Assert.Equal(HttpStatusCode.OK, opprettet.StatusCode);
        var ftbid = (await opprettet.Content.ReadFromJsonAsync<string>())!;
        request.Prosjektnavn = Guid.NewGuid().ToString();
        request.Eiendom = [new() { Eiendomsidentifikasjon = new("0301", 999, 1) }];
        var nytt = await client.PostAsJsonAsync("/api/ftbid", request);
        Assert.Equal(HttpStatusCode.OK, nytt.StatusCode);
        Assert.NotEqual(ftbid, await nytt.Content.ReadFromJsonAsync<string>());
    }

    [Fact]
    public async Task SamtidigeLignendeNavnTillates()
    {
        using var client = fixture.CreateClient();
        var name = "Tilbygg skole " + Guid.NewGuid().ToString("N");
        var response = await Task.WhenAll(client.PostAsJsonAsync("/api/ftbid", NyttProsjekt(name)),
            client.PostAsJsonAsync("/api/ftbid", NyttProsjekt(name + "e")));
        Assert.All(response, s => Assert.Equal(HttpStatusCode.OK, s.StatusCode));
    }

    [Theory]
    [InlineData("navn")]
    [InlineData("nummer")]
    [InlineData("type")]
    [InlineData("kommune")]
    [InlineData("gard")]
    [InlineData("bruk")]
    public async Task EttAvvikIUnikhetsnokkelenTillaterOppretting(string field)
    {
        using var client = fixture.CreateClient();
        var prosjekt = NyttProsjekt();
        prosjekt.Eiendom.RemoveAt(1);
        var opprettet = await client.PostAsJsonAsync("/api/ftbid", prosjekt);
        Assert.Equal(HttpStatusCode.OK, opprettet.StatusCode);
        var ftbid = await opprettet.Content.ReadFromJsonAsync<string>();
        if (field == "navn") prosjekt.Prosjektnavn += "e";
        if (field == "nummer") prosjekt.Prosjektnr += "2";
        if (field == "type") prosjekt.Tiltakstyper = ["Riving"];
        var ident = prosjekt.Eiendom[0].Eiendomsidentifikasjon!;
        prosjekt.Eiendom[0].Eiendomsidentifikasjon = field switch {
            "kommune" => ident with { Kommunenummer = "5001" },
            "gard" => ident with { Gaardsnummer = 11 },
            "bruk" => ident with { Bruksnummer = 2 }, _ => ident };
        var nytt = await client.PostAsJsonAsync("/api/ftbid", prosjekt);
        Assert.Equal(HttpStatusCode.OK, nytt.StatusCode);
        Assert.NotEqual(ftbid, await nytt.Content.ReadFromJsonAsync<string>());
    }

    [Theory]
    [InlineData("Prosjektnavn", null)]
    [InlineData("Prosjektnavn", "")]
    [InlineData("Prosjektnavn", "   ")]
    [InlineData("Prosjektnr", null)]
    [InlineData("Prosjektnr", "")]
    [InlineData("Prosjektnr", "   ")]
    [InlineData("Tiltakstyper", null)]
    [InlineData("Tiltakstyper", "")]
    [InlineData("Tiltakstyper", "   ")]
    [InlineData("Eiendom", null)]
    [InlineData("Eiendom", "tom")]
    public async Task UnikhetsopplysningerErPaakrevd(string field, string? value)
    {
        using var client = fixture.CreateClient();
        var prosjekt = NyttProsjekt();
        if (field == "Prosjektnavn") prosjekt.Prosjektnavn = value;
        if (field == "Prosjektnr") prosjekt.Prosjektnr = value;
        if (field == "Tiltakstyper") prosjekt.Tiltakstyper = value is null ? null! : [value];
        if (field == "Eiendom") prosjekt.Eiendom = value is null ? null! : [];
        await AssertProblemAsync(await client.PostAsJsonAsync("/api/ftbid", prosjekt), 400,
            (field is "Eiendom" or "Tiltakstyper") && value is null ? "V0038" : "V0042");
    }

    [Theory]
    [InlineData("Kommunenummer")]
    [InlineData("Gaardsnummer")]
    [InlineData("Bruksnummer")]
    public async Task MatrikkelfelterKanIkkeUtelatesEllerVaereNull(string field)
    {
        using var client = fixture.CreateClient();
        foreach (var nullverdi in new[] { false, true })
        {
            var json = JsonSerializer.SerializeToNode(NyttProsjekt())!;
            var matrikkel = json["Eiendom"]![0]!["Eiendomsidentifikasjon"]!.AsObject();
            if (nullverdi) matrikkel[field] = null;
            else matrikkel.Remove(field);
            await AssertProblemAsync(await client.PostAsJsonAsync("/api/ftbid", json), 400, "V0038");
        }
    }

    [Theory]
    [InlineData("prosjektnavn=INGENTREFF-00000000000000000000000000000000")]
    [InlineData("ftid=99999999")]
    public async Task SokUtenTreffGir404(string filter)
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/ftbid?" + filter);
        await AssertProblemAsync(response, 404, "V0072");
        Assert.Null(response.Headers.Location);
        Assert.Contains("Ingen ftbider funnet", await response.Content.ReadAsStringAsync());
    }

    private static async Task AssertDublettAsync(HttpResponseMessage response, string ftbid, string kode)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(kode, problem.RootElement.GetProperty("ftbidFeil").GetProperty("Kode").GetString());
        Assert.Equal(ftbid, problem.RootElement.GetProperty("eksisterendeFtbid").GetString());
        Assert.False(problem.RootElement.TryGetProperty("sannsynligDublett", out _));
        Assert.NotEmpty(problem.RootElement.GetProperty("samsvarendeFelter").EnumerateArray());
    }

    /// <summary>Søker i sekundæradresser og krever at matrikkelfiltre treffer samme enhet.</summary>
    [Fact]
    public async Task SokTrefferFlereVerdierUtenAaBlandeMatrikkelenheter()
    {
        using var client = fixture.CreateClient();
        var request = NyttProsjekt();
        request.Eiendom[0].Adresse = [new("Unik " + Guid.NewGuid().ToString("N"))];
        var response = await client.PostAsJsonAsync("/api/ftbid", request);
        var ftbid = await response.Content.ReadFromJsonAsync<string>();
        var sekundar = await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid?kommunenummer=5001&gaardsnummer=20&prosjektnavn=" + Uri.EscapeDataString(request.Prosjektnavn!));
        Assert.Equal(ftbid, Assert.Single(sekundar!).Ftbid);
        var adresse = await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid?adresse=" + Uri.EscapeDataString(request.Eiendom[0].Adresse[0].Adresselinje1!.ToUpperInvariant()));
        Assert.Equal(ftbid, Assert.Single(adresse!).Ftbid);
        var blandet = await client.GetAsync("/api/ftbid?kommunenummer=0301&gaardsnummer=20&prosjektnavn=" + Uri.EscapeDataString(request.Prosjektnavn!));
        await AssertProblemAsync(blandet, 404, "V0072");
    }

    /// <summary>Tilknytning er idempotent og kan flyttes til en annen eksisterende ftbid.</summary>
    [Fact]
    public async Task TilknytningHenterSkjemadataOgKanFlyttes()
    {
        using var client = fixture.CreateClient();
        var forste = await (await client.PostAsJsonAsync("/api/ftbid", NyttProsjekt())).Content.ReadFromJsonAsync<string>();
        var andre = await (await client.PostAsJsonAsync("/api/ftbid", NyttProsjekt())).Content.ReadFromJsonAsync<string>();
        foreach (var ftbid in new[] { forste, forste![^8..], andre })
        {
            var response = await client.PostAsJsonAsync($"/api/ftbid/{ftbid}/tilknytninger", new { Tilknytninger = new[] { new { ReferanseId = "urn:dibk:bygg:innsending-id:123456789", Typebeskrivelse = "Søknad om rammetillatelse", Tiltakstyper = new[] { "nyttanlegg" } } } });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
        var gammel = Assert.Single((await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid/" + forste))!);
        Assert.Empty(gammel.Tilknytninger);
        var ny = Assert.Single((await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid/" + andre))!);
        var skjema = Assert.Single(ny.Tilknytninger);
        Assert.Equal("Søknad om rammetillatelse", skjema.Typebeskrivelse);
        Assert.Equal(["nyttanlegg"], skjema.Tiltakstyper);
    }

    [Fact]
    public async Task TilknytningBrukerFtbidFraRuten()
    {
        using var client = fixture.CreateClient();
        var ftbid = (await (await client.PostAsJsonAsync("/api/ftbid", NyttProsjekt())).Content.ReadFromJsonAsync<string>())!;
        var referanse = "urn:dibk:test:" + Guid.NewGuid().ToString("N");
        using var response = await client.PostAsJsonAsync($"/api/ftbid/{ftbid}/tilknytninger", new
        {
            Ftbid = "709999900099999990",
            Tilknytninger = new[] { new { ReferanseId = referanse } }
        });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var prosjekt = Assert.Single((await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid/" + ftbid))!);
        Assert.Equal(referanse, Assert.Single(prosjekt.Tilknytninger).ReferanseId);
    }

    [Theory]
    [InlineData("AR123")]
    [InlineData("https://example.org/skjema")]
    [InlineData("urn:dibk:")]
    [InlineData("urn:dibk:ugyldig%XZ")]
    [InlineData("urn:dibk:med mellomrom")]
    public async Task UgyldigUrnGirTydeligValideringsfeil(string referanseId)
    {
        using var client = fixture.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/ftbid/709999900000000017/tilknytninger",
            new { Tilknytninger = new[] { new { ReferanseId = referanseId } } });
        await AssertProblemAsync(response, 400, "V0073");
        var tekst = await response.Content.ReadAsStringAsync();
        Assert.Contains("ReferanseId", tekst);
        Assert.Contains("URN", tekst);
    }

    [Fact]
    public async Task SkjemaRapportOgHistoriskReferanseReturneresMedNyStruktur()
    {
        using var client = fixture.CreateClient();
        var ftbid = (await (await client.PostAsJsonAsync("/api/ftbid", NyttProsjekt())).Content.ReadFromJsonAsync<string>())!;
        var skjemaId = "urn:dibk:bygg:innsending-id:" + Guid.NewGuid().ToString("N");
        var rapportId = "urn:dibk:bygg:rapport-id:" + Guid.NewGuid().ToString("N");
        var gammelId = "AR" + Guid.NewGuid().ToString("N");
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FtbidDbContext>();
            database.Skjemametadata.Add(new Skjemametadata { Arkivreferanse = gammelId });
            database.Identifikasjonsmetadata.Add(new Identifikasjonsmetadata { Arkivreferanse = gammelId, Ftbid = ftbid });
            await database.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/ftbid/{ftbid}/tilknytninger",
            new TilknyttFtbidRequest
            {
                Tilknytninger =
                [
                    new() { ReferanseId = skjemaId, Typebeskrivelse = "Søknad", Tiltakstyper = ["nybygg"] },
                    new() { ReferanseId = rapportId, Typebeskrivelse = "Rapport bygningsavfall", Tiltakstyper = ["nybygg"] }
                ]
            })).StatusCode);

        foreach (var url in new[] { "/api/ftbid/" + ftbid, "/api/ftbid?ftid=" + ftbid })
        {
            var json = await client.GetStringAsync(url);
            using var document = JsonDocument.Parse(json);
            var prosjekt = document.RootElement[0];
            Assert.False(prosjekt.TryGetProperty("TilknyttedeSkjemaer", out _));
            var tilknytninger = prosjekt.GetProperty("Tilknytninger");
            Assert.Equal(3, tilknytninger.GetArrayLength());
            foreach (var tilknytning in tilknytninger.EnumerateArray())
            {
                Assert.True(tilknytning.TryGetProperty("ReferanseId", out _));
                Assert.True(tilknytning.TryGetProperty("Typebeskrivelse", out _));
                Assert.True(tilknytning.TryGetProperty("Tiltakstyper", out _));
                Assert.False(tilknytning.TryGetProperty("Arkivreferanse", out _));
                Assert.False(tilknytning.TryGetProperty("Skjematype", out _));
            }
            var result = Assert.Single(JsonSerializer.Deserialize<List<FtbidResult>>(json)!);
            Assert.Equal("Rapport bygningsavfall", result.Tilknytninger.Single(t => t.ReferanseId == rapportId).Typebeskrivelse);
            Assert.Equal(new[] { "nybygg" }, result.Tilknytninger.Single(t => t.ReferanseId == skjemaId).Tiltakstyper);
            Assert.Null(result.Tilknytninger.Single(t => t.ReferanseId == gammelId).Typebeskrivelse);
        }
    }

    [Fact]
    public async Task UgyldigListeEndrerIngenMetadataEllerTilknytninger()
    {
        using var client = fixture.CreateClient();
        var ftbid = (await (await client.PostAsJsonAsync("/api/ftbid", NyttProsjekt())).Content.ReadFromJsonAsync<string>())!;
        var referanse = "urn:dibk:test:" + Guid.NewGuid().ToString("N");
        using var response = await client.PostAsJsonAsync($"/api/ftbid/{ftbid}/tilknytninger", new TilknyttFtbidRequest
        {
            Tilknytninger =
            [
                new() { ReferanseId = referanse, Typebeskrivelse = "Skal ikke lagres", Tiltakstyper = ["nybygg"] },
                new() { ReferanseId = "ugyldig" }
            ]
        });
        await AssertProblemAsync(response, 400, "V0073");
        var prosjekt = Assert.Single((await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid/" + ftbid))!);
        Assert.Empty(prosjekt.Tilknytninger);
        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<FtbidDbContext>();
        Assert.Null(await database.Skjemametadata.FindAsync(referanse));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Tilknytninger\":null}")]
    [InlineData("{\"Tilknytninger\":[]}")]
    [InlineData("{\"Tilknytninger\":[null]}")]
    public async Task ManglendeEllerTomTilknytningslisteGir400(string json)
    {
        using var client = fixture.CreateClient();
        var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;
        await AssertProblemAsync(await client.PostAsJsonAsync("/api/ftbid/709999900000000017/tilknytninger", data), 400, "V0042");
    }

    /// <summary>Registrerer nye referanser, men avviser ukjente ftbider.</summary>
    [Fact]
    public async Task NyeReferanserRegistreresOgUkjentFtbidAvvises()
    {
        using var client = fixture.CreateClient();
        var ftbid = await (await client.PostAsJsonAsync("/api/ftbid", NyttProsjekt())).Content.ReadFromJsonAsync<string>();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/ftbid/{ftbid}/tilknytninger", new { Tilknytninger = new[] { new { ReferanseId = "urn:dibk:ny:" + Guid.NewGuid().ToString("N") } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/ftbid/709999900099999990/tilknytninger", new { Tilknytninger = new[] { new { ReferanseId = "urn:dibk:bygg:innsending-id:123456789", Typebeskrivelse = "Søknad om rammetillatelse", Tiltakstyper = new[] { "nyttanlegg" } } } })).StatusCode);
    }

    /// <summary>Avviser ugyldige opprettingsdata og identifikatorer på norsk.</summary>
    [Fact]
    public async Task UgyldigeDataGir400()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/ftbid/ugyldig")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/ftbid", new { })).StatusCode);
        foreach (var kommune in new[] { "9999", "9998", "abc" })
        {
            var request = NyttProsjekt(); request.Eiendom[0].Eiendomsidentifikasjon = request.Eiendom[0].Eiendomsidentifikasjon! with { Kommunenummer = kommune };
            var response = await client.PostAsJsonAsync("/api/ftbid", request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("kommunenummer", (await response.Content.ReadAsStringAsync()).ToLowerInvariant());
        }
        var ugyldig = NyttProsjekt(); ugyldig.Eiendom[0].Eiendomsidentifikasjon = ugyldig.Eiendom[0].Eiendomsidentifikasjon! with { Bruksnummer = -1 };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/ftbid", ugyldig)).StatusCode);
        using var innhold = new StringContent("null", System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/ftbid", innhold)).StatusCode);
    }

    /// <summary>Ukjente, velformede ftbider returnerer en tom liste som i adapteren.</summary>
    [Fact]
    public async Task UkjentFtbidGirTomListe()
    {
        using var client = fixture.CreateClient();
        Assert.Empty((await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid/99999999"))!);
    }

    /// <summary>Validerer ekte JWT-signaturer, levetid, utsteder og konsument.</summary>
    [Theory]
    [InlineData("mangler")]
    [InlineData("utlopt")]
    [InlineData("feil_utsteder")]
    [InlineData("feil_signatur")]
    [InlineData("uten_konsument")]
    [InlineData("ugyldig_konsument")]
    public async Task UgyldigTokenGir401(string variant)
    {
        using var client = fixture.CreateClient(variant: variant);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/ftbid?kommunenummer=0301")).StatusCode);
    }

    /// <summary>Les- og endrescope gir tilgang til hver sin operasjon.</summary>
    [Fact]
    public async Task ScopesKontrolleresPerEndepunkt()
    {
        using var leser = fixture.CreateClient("dibk:bygg/ftbid.les");
        using var skriver = fixture.CreateClient("dibk:bygg/ftbid.endre");
        using var error = fixture.CreateClient("dibk:bygg/ftbid.les.ekstra");
        Assert.Equal(HttpStatusCode.OK, (await leser.GetAsync("/api/ftbid/99999999")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await leser.PostAsJsonAsync("/api/ftbid", NyttProsjekt())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await leser.PostAsJsonAsync("/api/ftbid/709999900000000017/tilknytninger", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await skriver.GetAsync("/api/ftbid/99999999")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await error.GetAsync("/api/ftbid/99999999")).StatusCode);
    }

    /// <summary>Oppretting begrenses til 200 forespørsler per minutt per konsument.</summary>
    [Fact]
    public async Task RatebegrensningErPerKonsument()
    {
        using var client = fixture.CreateClient(orgnummer: "987654321");
        for (var i = 0; i < 200; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/ftbid", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/ftbid", new { })).StatusCode);
        using var annen = fixture.CreateClient(orgnummer: "111222333");
        Assert.Equal(HttpStatusCode.BadRequest, (await annen.PostAsJsonAsync("/api/ftbid", new { })).StatusCode);
    }

    /// <summary>Helse og OpenAPI kan leses uten token, og OpenAPI beskriver bearer-autentisering.</summary>
    [Fact]
    public async Task HelseOgOpenApiErTilgjengelige()
    {
        using var client = fixture.CreateClient(variant: "mangler");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/helse")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/helse/levende")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var stier = document.RootElement.GetProperty("paths");
        Assert.Equal(6, stier.EnumerateObject().Count());
        Assert.True(stier.GetProperty("/api/ftbid/eiendom").TryGetProperty("get", out _));
        Assert.False(stier.TryGetProperty("/api/ftbid/liste", out _));
        Assert.False(stier.TryGetProperty("/api/eiendom", out _));
        var svarskjema = stier.GetProperty("/api/ftbid").GetProperty("get").GetProperty("responses")
            .GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal(2, svarskjema.GetProperty("oneOf").GetArrayLength());
        Assert.False(stier.GetProperty("/api/ftbid/eiendom").GetProperty("get").GetProperty("responses").TryGetProperty("204", out _));
        Assert.True(stier.GetProperty("/api/ftbid/{ftbid}/eiendom").TryGetProperty("post", out _));
        Assert.True(stier.GetProperty("/api/ftbid/{ftbid}/eiendom/{eiendomId}").TryGetProperty("put", out _));
        Assert.True(stier.GetProperty("/api/ftbid/{ftbid}/eiendom/{eiendomId}").TryGetProperty("delete", out _));
        Assert.True(stier.GetProperty("/api/ftbid").TryGetProperty("get", out _));
        Assert.False(stier.TryGetProperty("/api/ftbid/sok", out _));
        Assert.True(stier.GetProperty("/api/ftbid/{ftbid}/tilknytninger").TryGetProperty("post", out _));
        Assert.False(stier.TryGetProperty("/api/ftbid/tilknytt", out _));
        Assert.False(stier.TryGetProperty("/api/ftbid/search", out _));
        Assert.False(stier.TryGetProperty("/api/ftbid/attach", out _));
        Assert.True(stier.GetProperty("/api/ftbid").GetProperty("post").GetProperty("responses")
            .GetProperty("400").GetProperty("content").TryGetProperty("application/problem+json", out _));
        var felter = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("TilknyttFtbidRequest").GetProperty("properties");
        Assert.True(felter.TryGetProperty("Tilknytninger", out _));
        Assert.False(felter.TryGetProperty("Ftbid", out _));
        Assert.False(felter.TryGetProperty("ReferanseId", out _));
        Assert.Equal("array", felter.GetProperty("Tilknytninger").GetProperty("type").GetString());
        Assert.Equal(1, felter.GetProperty("Tilknytninger").GetProperty("minItems").GetInt32());
        var inndata = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("TilknytningRequest").GetProperty("properties");
        Assert.False(felter.TryGetProperty("ArchiveReference", out _));
        Assert.False(felter.TryGetProperty("Arkivreferanse", out _));
        Assert.Equal(Ftpb.Ftbid.Application.Validation.UrnValidation.Pattern,
            inndata.GetProperty("ReferanseId").GetProperty("pattern").GetString());
        Assert.Equal(80, inndata.GetProperty("ReferanseId").GetProperty("maxLength").GetInt32());
        var skjemaer = document.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(skjemaer.GetProperty("FtbidResult").GetProperty("properties").TryGetProperty("Tilknytninger", out _));
        Assert.Equal("date-time", skjemaer.GetProperty("FtbidResult").GetProperty("properties")
            .GetProperty("Utstedt").GetProperty("format").GetString());
        var tilknytning = skjemaer.GetProperty("Tilknytning").GetProperty("properties");
        Assert.True(tilknytning.TryGetProperty("Typebeskrivelse", out _));
        Assert.True(tilknytning.TryGetProperty("Tiltakstyper", out _));
        Assert.False(tilknytning.GetProperty("ReferanseId").TryGetProperty("pattern", out _));
        Assert.Equal("bearer", document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Maskinporten").GetProperty("scheme").GetString());
    }

    /// <summary>Databaseberedskap gir 503 når PostgreSQL ikke kan nås; prosesshelse fortsetter å svare.</summary>
    [Fact]
    public async Task UtilgjengeligDatabaseGirUsunnHelse()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("Database:MigrerVedOppstart", "false").ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<FtbidDbContext>>();
            services.AddDbContext<FtbidDbContext>(options => options.UseNpgsql("Host=127.0.0.1;Port=1;Database=utilgjengelig;Username=ftbid;Timeout=1"));
        }));
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/helse")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/helse/levende")).StatusCode);
    }

    /// <summary>Produksjonsmiljøet tilbyr helse, men eksponerer ikke Swagger eller OpenAPI.</summary>
    [Fact]
    public async Task SwaggerErAvskruddIProduksjon()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(b => b.UseEnvironment("Production"));
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/helse")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/index.html")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }

    /// <summary>Importen kan gjentas og oppdatere referansedata uten duplikater.</summary>
    [Fact]
    public async Task ReferansedataImportErIdempotent()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<FtbidDbContext>();
        var fil = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(fil, """
                { "Kommuner": [{ "Kommunenummer": "1103" }],
                  "Skjemaer": [{ "Arkivreferanse": "ARIMPORT", "Skjematype": "Første", "Tiltakstyper": ["nyttanlegg"] }] }
                """);
            await scope.ServiceProvider.GetRequiredService<IReferansedataImportService>().ImportAsync(fil);
            await File.WriteAllTextAsync(fil, """
                { "Kommuner": [{ "Kommunenummer": "1103" }],
                  "Skjemaer": [{ "Arkivreferanse": "ARIMPORT", "Skjematype": "Oppdatert", "Tiltakstyper": ["rivinganlegg"] }] }
                """);
            await scope.ServiceProvider.GetRequiredService<IReferansedataImportService>().ImportAsync(fil);
            Assert.Equal(1, await database.Kommuner.CountAsync(k => k.Kommunenummer == "1103"));
            var skjema = await database.Skjemametadata.SingleAsync(m => m.Arkivreferanse == "ARIMPORT");
            Assert.Equal("Oppdatert", skjema.Skjematype);
            Assert.Equal(["rivinganlegg"], skjema.Tiltakstyper);
        }
        finally { File.Delete(fil); }
    }


    /// <summary>Forretningsregler returnerer stabile NIREG-koder i standard ProblemDetails.</summary>
    [Fact]
    public async Task RegelbruddReturnererProblemDetailsMedKode()
    {
        using var client = fixture.CreateClient();
        var kommune = NyttProsjekt(); kommune.Eiendom[0].Eiendomsidentifikasjon = kommune.Eiendom[0].Eiendomsidentifikasjon! with { Kommunenummer = "9999" };
        var response = await client.PostAsJsonAsync("/api/ftbid", kommune);
        await AssertProblemAsync(response, 400, "V0034");
        await AssertProblemAsync(await client.PostAsJsonAsync("/api/ftbid", new { }), 400, "V0042");
        await AssertProblemAsync(await client.GetAsync("/api/ftbid/ugyldig"), 400, "V0052");
        await AssertProblemAsync(await client.PostAsJsonAsync("/api/ftbid/709999900099999990/tilknytninger",
            new { Tilknytninger = new[] { new { ReferanseId = "urn:dibk:bygg:innsending-id:123456789", Typebeskrivelse = "Søknad om rammetillatelse", Tiltakstyper = new[] { "nyttanlegg" } } } }), 400, "V0054");
        using var innhold = new StringContent("{ugyldig", System.Text.Encoding.UTF8, "application/json");
        await AssertProblemAsync(await client.PostAsync("/api/ftbid", innhold), 400, "V0038");
    }

    /// <summary>Autentiseringsfeil bruker også Core-mellomvarens ProblemDetails-format.</summary>
    [Fact]
    public async Task AutentiseringsfeilReturnererProblemDetails()
    {
        using var client = fixture.CreateClient(variant: "mangler");
        var response = await client.GetAsync("/api/ftbid/99999999");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(401, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(JsonValueKind.Null, problem.RootElement.GetProperty("detail").ValueKind);
        Assert.Equal("Autentisering kreves", problem.RootElement.GetProperty("title").GetString());
        Assert.True(problem.RootElement.TryGetProperty("sporingsidentifikator", out _));
    }

    /// <summary>Uventede feil skjuler intern informasjon og returnerer HTTP 500 med ProblemDetails.</summary>
    [Fact]
    public async Task InternFeilSkjulerDatabaseopplysninger()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("Database:MigrerVedOppstart", "false").ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<FtbidDbContext>>();
            services.AddDbContext<FtbidDbContext>(options => options.UseNpgsql("Host=127.0.0.1;Port=1;Database=hemmelig_database;Username=hemmelig_bruker;Timeout=1"));
        }));
        using var client = factory.CreateClient();
        using var autorisert = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = autorisert.DefaultRequestHeaders.Authorization;
        var response = await client.GetAsync("/api/ftbid/99999999");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var innhold = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hemmelig_", innhold);
        using var problem = JsonDocument.Parse(innhold);
        Assert.Equal(500, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(problem.RootElement.TryGetProperty("ftbidFeil", out _));
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, int status, string kode)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(status, problem.RootElement.GetProperty("status").GetInt32());
        Assert.True(problem.RootElement.TryGetProperty("type", out _));
        Assert.True(problem.RootElement.TryGetProperty("title", out _));
        Assert.True(problem.RootElement.TryGetProperty("detail", out _));
        Assert.True(problem.RootElement.TryGetProperty("instance", out _));
        Assert.True(problem.RootElement.TryGetProperty("sporingsidentifikator", out _));
        Assert.Equal(kode, problem.RootElement.GetProperty("ftbidFeil").GetProperty("Kode").GetString());
        Assert.Equal(0, problem.RootElement.GetProperty("ftbidFeil").GetProperty("Type").GetInt32());
    }
}
