using Ftpb.Ftbid.Tests.Oppsett;
using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Domain.Referansedata;
using Ftpb.Ftbid.Infrastructure.Persistens;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Ftpb.Ftbid.Tests.Integrasjon;

/// <summary>Verifiserer API-kontrakt, PostgreSQL-lagring, Maskinporten og helsesjekker.</summary>
[Collection("Postgres")]
public sealed class FtbidApiTester(PostgresApiOppsett oppsett)
{
    private static OpprettFtbid NyttProsjekt(string? navn = null) => new()
    {
        Kommunenummer = "0301", Gaardsnummer = 10, Bruksnummer = 1,
        Prosjektnavn = navn ?? "Prosjekt " + Guid.NewGuid().ToString("N"), System = "Testsystem",
        Adresse = "Karl Johans gate 1", Adresser = ["Storgata 2"],
        Matrikkelopplysninger = [new("5001", 20, 2, 3, 4)]
    };

    /// <summary>Bevarer eldre enkeltfelter og returnerer flere adresser og matrikkelenheter.</summary>
    [Fact]
    public async Task OpprettingOgOppslagBevarerModellen()
    {
        using var klient = oppsett.Klient();
        var foresporsel = NyttProsjekt();
        var svar = await klient.PostAsJsonAsync("/api/ftbid", foresporsel);
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var ftbid = (await svar.Content.ReadFromJsonAsync<string>())!;
        Assert.Equal(18, ftbid.Length);
        Assert.StartsWith("7099999000", ftbid);
        var resultater = await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/" + ftbid);
        var resultat = Assert.Single(resultater!);
        Assert.Equal(foresporsel.Prosjektnavn, resultat.Prosjektnavn);
        Assert.Equal("0301", resultat.Kommunenummer);
        Assert.Equal(2, resultat.Adresser.Count);
        Assert.Equal(2, resultat.Matrikkelopplysninger.Count);
        var kort = await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/" + ftbid[^8..]);
        Assert.Equal(ftbid, Assert.Single(kort!).Ftbid);
        var json = await klient.GetStringAsync("/api/ftbid/" + ftbid);
        Assert.Contains("\"Ftbid\"", json);
        Assert.Contains("\"TilknyttedeSkjemaer\"", json);
    }

    /// <summary>Støtter oppretting med kun de nye listene.</summary>
    [Fact]
    public async Task OpprettingMedKunListerFungerer()
    {
        using var klient = oppsett.Klient();
        var foresporsel = new OpprettFtbid { Prosjektnavn = Guid.NewGuid().ToString(), System = "Test",
            Adresser = ["Adresse 1", "Adresse 2"], Matrikkelopplysninger = [new("0301", 1, 2), new("5001", 3, 4)] };
        var svar = await klient.PostAsJsonAsync("/api/ftbid", foresporsel);
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var ftbid = await svar.Content.ReadFromJsonAsync<string>();
        var resultat = Assert.Single((await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/" + ftbid))!);
        Assert.Equal("Adresse 1", resultat.Adresse);
        Assert.Equal(2, resultat.Matrikkelopplysninger.Count);
    }

    /// <summary>Kun én samtidig oppretting lykkes; dubletter returnerer 409 uten å endre prosjektet.</summary>
    [Fact]
    public async Task SamtidigOpprettingGirEnFtbidOgDublettkonflikter()
    {
        using var klient = oppsett.Klient();
        var foresporsel = NyttProsjekt();
        var svar = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => klient.PostAsJsonAsync("/api/ftbid", foresporsel)));
        var opprettet = Assert.Single(svar, s => s.StatusCode == HttpStatusCode.OK);
        var ftbid = await opprettet.Content.ReadFromJsonAsync<string>();
        foreach (var dublett in svar.Where(s => s != opprettet))
            await KontrollerDublettAsync(dublett, ftbid!, "V0067", false);
        foresporsel.Prosjektnavn = "  " + foresporsel.Prosjektnavn!.ToUpperInvariant() + "  ";
        foresporsel.Prosjektnr = "P-1001";
        await KontrollerDublettAsync(await klient.PostAsJsonAsync("/api/ftbid", foresporsel), ftbid!, "V0067", false);
        var resultat = Assert.Single((await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/" + ftbid))!);
        Assert.Null(resultat.Prosjektnummer);
        using var omfang = oppsett.Fabrikk.Services.CreateScope();
        Assert.Equal(1, await omfang.ServiceProvider.GetRequiredService<FtbidDbContext>().Ftbider
            .CountAsync(f => f.Ean == ftbid || f.Prosjektnummer == "P-1001"));
    }

    [Fact]
    public async Task NyeOpplysningerLagresOgSannsynligDublettAvvises()
    {
        using var klient = oppsett.Klient();
        var foresporsel = NyttProsjekt();
        foresporsel.EksternProsjektreferanse = Guid.NewGuid().ToString();
        foresporsel.KommunaltSaksnummer = Guid.NewGuid().ToString();
        foresporsel.Tiltakshaver = "Ola Nordmann";
        foresporsel.Organisasjonsnummer = "123456789";
        foresporsel.Bygningsnummer = Guid.NewGuid().ToString();
        foresporsel.Tiltakstype = "Tilbygg";
        var svar = await klient.PostAsJsonAsync("/api/ftbid", foresporsel);
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var ftbid = (await svar.Content.ReadFromJsonAsync<string>())!;
        var resultat = Assert.Single((await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/" + ftbid))!);
        Assert.Equal(foresporsel.EksternProsjektreferanse, resultat.EksternProsjektreferanse);
        Assert.Equal(foresporsel.KommunaltSaksnummer, resultat.KommunaltSaksnummer);
        Assert.Equal(foresporsel.Tiltakshaver, resultat.Tiltakshaver);
        Assert.Equal(foresporsel.Organisasjonsnummer, resultat.Organisasjonsnummer);
        Assert.Equal(foresporsel.Bygningsnummer, resultat.Bygningsnummer);
        Assert.Equal(foresporsel.Tiltakstype, resultat.Tiltakstype);
        foresporsel.EksternProsjektreferanse = null;
        foresporsel.KommunaltSaksnummer = null;
        foresporsel.Prosjektnavn = "Et helt annet navn";
        await KontrollerDublettAsync(await klient.PostAsJsonAsync("/api/ftbid", foresporsel), ftbid, "V0068", true);
    }

    [Theory]
    [InlineData("sak")]
    [InlineData("referanse")]
    public async Task ReferanserAvviserDublettMedAnnetNavnOgMatrikkel(string felt)
    {
        using var klient = oppsett.Klient();
        var foresporsel = NyttProsjekt();
        if (felt == "sak") foresporsel.KommunaltSaksnummer = Guid.NewGuid().ToString();
        else foresporsel.EksternProsjektreferanse = Guid.NewGuid().ToString();
        var opprettet = await klient.PostAsJsonAsync("/api/ftbid", foresporsel);
        Assert.Equal(HttpStatusCode.OK, opprettet.StatusCode);
        var ftbid = (await opprettet.Content.ReadFromJsonAsync<string>())!;
        foresporsel.Prosjektnavn = Guid.NewGuid().ToString();
        foresporsel.Gaardsnummer = 999;
        foresporsel.Matrikkelopplysninger = [];
        var dublett = await klient.PostAsJsonAsync("/api/ftbid", foresporsel);
        await KontrollerDublettAsync(dublett, ftbid, "V0067", false);
        var hentet = await klient.GetFromJsonAsync<List<FtbidResultat>>(dublett.Headers.Location);
        Assert.Equal(ftbid, Assert.Single(hentet!).Ftbid);
    }

    [Fact]
    public async Task SamtidigeSannsynligeDubletterOppretterBareEttProsjekt()
    {
        using var klient = oppsett.Klient();
        var navn = "Tilbygg skole " + Guid.NewGuid().ToString("N");
        var svar = await Task.WhenAll(klient.PostAsJsonAsync("/api/ftbid", NyttProsjekt(navn)),
            klient.PostAsJsonAsync("/api/ftbid", NyttProsjekt(navn + "e")));
        var opprettet = Assert.Single(svar, s => s.StatusCode == HttpStatusCode.OK);
        var ftbid = (await opprettet.Content.ReadFromJsonAsync<string>())!;
        await KontrollerDublettAsync(Assert.Single(svar, s => s.StatusCode == HttpStatusCode.Conflict), ftbid, "V0068", true);
    }

    private static async Task KontrollerDublettAsync(HttpResponseMessage svar, string ftbid, string kode, bool sannsynlig)
    {
        Assert.Equal(HttpStatusCode.Conflict, svar.StatusCode);
        Assert.Equal("/api/ftbid/" + ftbid, svar.Headers.Location?.OriginalString);
        Assert.Equal("application/problem+json", svar.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await svar.Content.ReadAsStringAsync());
        Assert.Equal(kode, problem.RootElement.GetProperty("ftbidFeil").GetProperty("Kode").GetString());
        Assert.Equal(ftbid, problem.RootElement.GetProperty("eksisterendeFtbid").GetString());
        Assert.Equal(sannsynlig, problem.RootElement.GetProperty("sannsynligDublett").GetBoolean());
        Assert.NotEmpty(problem.RootElement.GetProperty("samsvarendeFelter").EnumerateArray());
    }

    /// <summary>Søker i sekundæradresser og krever at matrikkelfiltre treffer samme enhet.</summary>
    [Fact]
    public async Task SokTrefferFlereVerdierUtenAaBlandeMatrikkelenheter()
    {
        using var klient = oppsett.Klient();
        var foresporsel = NyttProsjekt();
        foresporsel.Adresser = ["Unik " + Guid.NewGuid().ToString("N")];
        var svar = await klient.PostAsJsonAsync("/api/ftbid", foresporsel);
        var ftbid = await svar.Content.ReadFromJsonAsync<string>();
        var sekundar = await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/sok?kommunenummer=5001&gaardsnummer=20&prosjektnavn=" + Uri.EscapeDataString(foresporsel.Prosjektnavn!));
        Assert.Equal(ftbid, Assert.Single(sekundar!).Ftbid);
        var adresse = await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/sok?adresse=" + Uri.EscapeDataString(foresporsel.Adresser[0].ToUpperInvariant()));
        Assert.Equal(ftbid, Assert.Single(adresse!).Ftbid);
        var blandet = await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/sok?kommunenummer=0301&gaardsnummer=20&prosjektnavn=" + Uri.EscapeDataString(foresporsel.Prosjektnavn!));
        Assert.Empty(blandet!);
    }

    /// <summary>Tilknytning er idempotent og kan flyttes til en annen eksisterende ftbid.</summary>
    [Fact]
    public async Task TilknytningHenterSkjemadataOgKanFlyttes()
    {
        using var klient = oppsett.Klient();
        var forste = await (await klient.PostAsJsonAsync("/api/ftbid", NyttProsjekt())).Content.ReadFromJsonAsync<string>();
        var andre = await (await klient.PostAsJsonAsync("/api/ftbid", NyttProsjekt())).Content.ReadFromJsonAsync<string>();
        foreach (var ftbid in new[] { forste, forste, andre })
        {
            var svar = await klient.PostAsJsonAsync("/api/ftbid/tilknytt", new { Ftbid = ftbid, Arkivreferanse = "AR123456789" });
            Assert.Equal(HttpStatusCode.NoContent, svar.StatusCode);
        }
        var gammel = Assert.Single((await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/" + forste))!);
        Assert.Empty(gammel.TilknyttedeSkjemaer);
        var ny = Assert.Single((await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/" + andre))!);
        var skjema = Assert.Single(ny.TilknyttedeSkjemaer);
        Assert.Equal("Søknad om rammetillatelse", skjema.Skjematype);
        Assert.Equal(["nyttanlegg"], skjema.Tiltakstyper);
    }

    /// <summary>Avviser tilknytning til ukjente ftbider eller arkivreferanser.</summary>
    [Fact]
    public async Task UkjenteTilknytningerAvvises()
    {
        using var klient = oppsett.Klient();
        var ftbid = await (await klient.PostAsJsonAsync("/api/ftbid", NyttProsjekt())).Content.ReadFromJsonAsync<string>();
        Assert.Equal(HttpStatusCode.BadRequest, (await klient.PostAsJsonAsync("/api/ftbid/tilknytt", new { Ftbid = ftbid, Arkivreferanse = "ukjent" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await klient.PostAsJsonAsync("/api/ftbid/tilknytt", new { Ftbid = "709999900099999990", Arkivreferanse = "AR123456789" })).StatusCode);
    }

    /// <summary>Avviser manglende filtre og ugyldige opprettingsdata på norsk.</summary>
    [Fact]
    public async Task UgyldigeDataGir400()
    {
        using var klient = oppsett.Klient();
        Assert.Equal(HttpStatusCode.BadRequest, (await klient.GetAsync("/api/ftbid/sok")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await klient.GetAsync("/api/ftbid/ugyldig")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await klient.PostAsJsonAsync("/api/ftbid", new { })).StatusCode);
        foreach (var kommune in new[] { "9999", "9998", "abc" })
        {
            var foresporsel = NyttProsjekt(); foresporsel.Kommunenummer = kommune;
            var svar = await klient.PostAsJsonAsync("/api/ftbid", foresporsel);
            Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
            Assert.Contains("kommunenummer", (await svar.Content.ReadAsStringAsync()).ToLowerInvariant());
        }
        var ugyldig = NyttProsjekt(); ugyldig.Bruksnummer = -1;
        Assert.Equal(HttpStatusCode.BadRequest, (await klient.PostAsJsonAsync("/api/ftbid", ugyldig)).StatusCode);
        using var innhold = new StringContent("null", System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await klient.PostAsync("/api/ftbid", innhold)).StatusCode);
    }

    /// <summary>Ukjente, velformede ftbider returnerer en tom liste som i adapteren.</summary>
    [Fact]
    public async Task UkjentFtbidGirTomListe()
    {
        using var klient = oppsett.Klient();
        Assert.Empty((await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/99999999"))!);
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
        using var klient = oppsett.Klient(variant: variant);
        Assert.Equal(HttpStatusCode.Unauthorized, (await klient.GetAsync("/api/ftbid/sok?kommunenummer=0301")).StatusCode);
    }

    /// <summary>Les- og endrescope gir tilgang til hver sin operasjon.</summary>
    [Fact]
    public async Task ScopesKontrolleresPerEndepunkt()
    {
        using var leser = oppsett.Klient("dibk:bygg/ftbid.les");
        using var skriver = oppsett.Klient("dibk:bygg/ftbid.endre");
        using var feil = oppsett.Klient("dibk:bygg/ftbid.les.ekstra");
        Assert.Equal(HttpStatusCode.OK, (await leser.GetAsync("/api/ftbid/sok?kommunenummer=0301")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await leser.PostAsJsonAsync("/api/ftbid", NyttProsjekt())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await leser.PostAsJsonAsync("/api/ftbid/tilknytt", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await skriver.GetAsync("/api/ftbid/99999999")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await feil.GetAsync("/api/ftbid/99999999")).StatusCode);
    }

    /// <summary>Oppretting begrenses til 200 forespørsler per minutt per konsument.</summary>
    [Fact]
    public async Task RatebegrensningErPerKonsument()
    {
        using var klient = oppsett.Klient(orgnummer: "987654321");
        for (var i = 0; i < 200; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await klient.PostAsJsonAsync("/api/ftbid", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await klient.PostAsJsonAsync("/api/ftbid", new { })).StatusCode);
        using var annen = oppsett.Klient(orgnummer: "111222333");
        Assert.Equal(HttpStatusCode.BadRequest, (await annen.PostAsJsonAsync("/api/ftbid", new { })).StatusCode);
    }

    /// <summary>Helse og OpenAPI kan leses uten token, og OpenAPI beskriver bearer-autentisering.</summary>
    [Fact]
    public async Task HelseOgOpenApiErTilgjengelige()
    {
        using var klient = oppsett.Klient(variant: "mangler");
        Assert.Equal(HttpStatusCode.OK, (await klient.GetAsync("/helse")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await klient.GetAsync("/helse/levende")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await klient.GetAsync("/swagger/index.html")).StatusCode);
        using var dokument = JsonDocument.Parse(await klient.GetStringAsync("/openapi/v1.json"));
        var stier = dokument.RootElement.GetProperty("paths");
        Assert.Equal(6, stier.EnumerateObject().Count());
        foreach (var navn in new[] { "adresser", "matrikkelopplysninger" })
        {
            var operasjoner = stier.GetProperty("/api/ftbid/{ftbid}/" + navn);
            Assert.True(operasjoner.TryGetProperty("post", out _));
            Assert.True(operasjoner.TryGetProperty("delete", out _));
            Assert.True(operasjoner.GetProperty("post").GetProperty("responses").TryGetProperty("204", out _));
        }
        Assert.True(stier.TryGetProperty("/api/ftbid/sok", out _));
        Assert.True(stier.TryGetProperty("/api/ftbid/tilknytt", out _));
        Assert.False(stier.TryGetProperty("/api/ftbid/search", out _));
        Assert.False(stier.TryGetProperty("/api/ftbid/attach", out _));
        Assert.True(stier.GetProperty("/api/ftbid").GetProperty("post").GetProperty("responses")
            .GetProperty("400").GetProperty("content").TryGetProperty("application/problem+json", out _));
        var felter = dokument.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("TilknyttFtbid").GetProperty("properties");
        Assert.True(felter.TryGetProperty("Arkivreferanse", out _));
        Assert.False(felter.TryGetProperty("ArchiveReference", out _));
        Assert.Equal("bearer", dokument.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Maskinporten").GetProperty("scheme").GetString());
    }

    /// <summary>Databaseberedskap gir 503 når PostgreSQL ikke kan nås; prosesshelse fortsetter å svare.</summary>
    [Fact]
    public async Task UtilgjengeligDatabaseGirUsunnHelse()
    {
        using var fabrikk = oppsett.Fabrikk.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<FtbidDbContext>>();
            services.AddDbContext<FtbidDbContext>(valg => valg.UseNpgsql("Host=127.0.0.1;Port=1;Database=utilgjengelig;Username=ftbid;Timeout=1"));
        }));
        using var klient = fabrikk.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await klient.GetAsync("/helse")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await klient.GetAsync("/helse/levende")).StatusCode);
    }

    /// <summary>Produksjonsmiljøet tilbyr helse, men eksponerer ikke Swagger eller OpenAPI.</summary>
    [Fact]
    public async Task SwaggerErAvskruddIProduksjon()
    {
        using var fabrikk = oppsett.Fabrikk.WithWebHostBuilder(b => b.UseEnvironment("Production"));
        using var klient = fabrikk.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await klient.GetAsync("/helse")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await klient.GetAsync("/swagger/index.html")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await klient.GetAsync("/openapi/v1.json")).StatusCode);
    }

    /// <summary>Importen kan gjentas og oppdatere referansedata uten duplikater.</summary>
    [Fact]
    public async Task ReferansedataImportErIdempotent()
    {
        using var omfang = oppsett.Fabrikk.Services.CreateScope();
        var database = omfang.ServiceProvider.GetRequiredService<FtbidDbContext>();
        var fil = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(fil, """
                { "Kommuner": [{ "Kommunenummer": "1103" }],
                  "Skjemaer": [{ "Arkivreferanse": "ARIMPORT", "Skjematype": "Første", "Tiltakstyper": ["nyttanlegg"] }] }
                """);
            await omfang.ServiceProvider.GetRequiredService<IReferansedataImportService>().ImporterAsync(fil);
            await File.WriteAllTextAsync(fil, """
                { "Kommuner": [{ "Kommunenummer": "1103" }],
                  "Skjemaer": [{ "Arkivreferanse": "ARIMPORT", "Skjematype": "Oppdatert", "Tiltakstyper": ["rivinganlegg"] }] }
                """);
            await omfang.ServiceProvider.GetRequiredService<IReferansedataImportService>().ImporterAsync(fil);
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
        using var klient = oppsett.Klient();
        var kommune = NyttProsjekt(); kommune.Kommunenummer = "9999";
        var svar = await klient.PostAsJsonAsync("/api/ftbid", kommune);
        await KontrollerProblemAsync(svar, 400, "V0034");
        await KontrollerProblemAsync(await klient.PostAsJsonAsync("/api/ftbid", new { }), 400, "V0042");
        await KontrollerProblemAsync(await klient.GetAsync("/api/ftbid/sok"), 400, "V0050");
        await KontrollerProblemAsync(await klient.GetAsync("/api/ftbid/ugyldig"), 400, "V0052");
        await KontrollerProblemAsync(await klient.PostAsJsonAsync("/api/ftbid/tilknytt",
            new { Ftbid = "709999900099999990", Arkivreferanse = "AR123456789" }), 400, "V0054");
        using var innhold = new StringContent("{ugyldig", System.Text.Encoding.UTF8, "application/json");
        await KontrollerProblemAsync(await klient.PostAsync("/api/ftbid", innhold), 400, "V0038");
    }

    /// <summary>Autentiseringsfeil bruker også Core-mellomvarens ProblemDetails-format.</summary>
    [Fact]
    public async Task AutentiseringsfeilReturnererProblemDetails()
    {
        using var klient = oppsett.Klient(variant: "mangler");
        var svar = await klient.GetAsync("/api/ftbid/99999999");
        Assert.Equal(HttpStatusCode.Unauthorized, svar.StatusCode);
        Assert.Equal("application/problem+json", svar.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await svar.Content.ReadAsStringAsync());
        Assert.Equal(401, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Autentisering kreves", problem.RootElement.GetProperty("title").GetString());
        Assert.True(problem.RootElement.TryGetProperty("sporingsidentifikator", out _));
    }

    /// <summary>Uventede feil skjuler intern informasjon og returnerer HTTP 500 med ProblemDetails.</summary>
    [Fact]
    public async Task InternFeilSkjulerDatabaseopplysninger()
    {
        using var fabrikk = oppsett.Fabrikk.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<FtbidDbContext>>();
            services.AddDbContext<FtbidDbContext>(valg => valg.UseNpgsql("Host=127.0.0.1;Port=1;Database=hemmelig_database;Username=hemmelig_bruker;Timeout=1"));
        }));
        using var klient = fabrikk.CreateClient();
        using var autorisert = oppsett.Klient();
        klient.DefaultRequestHeaders.Authorization = autorisert.DefaultRequestHeaders.Authorization;
        var svar = await klient.GetAsync("/api/ftbid/99999999");
        Assert.Equal(HttpStatusCode.InternalServerError, svar.StatusCode);
        Assert.Equal("application/problem+json", svar.Content.Headers.ContentType?.MediaType);
        var innhold = await svar.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hemmelig_", innhold);
        using var problem = JsonDocument.Parse(innhold);
        Assert.Equal(500, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(problem.RootElement.TryGetProperty("ftbidFeil", out _));
    }

    private static async Task KontrollerProblemAsync(HttpResponseMessage svar, int status, string kode)
    {
        Assert.Equal(status, (int)svar.StatusCode);
        Assert.Equal("application/problem+json", svar.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await svar.Content.ReadAsStringAsync());
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
