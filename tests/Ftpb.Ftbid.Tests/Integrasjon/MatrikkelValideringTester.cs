using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Matrikkel;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using Ftpb.Ftbid.Infrastructure.Persistens;
using Ftpb.Ftbid.Tests.Oppsett;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Ftpb.Ftbid.Tests.Integrasjon;

/// <summary>Kontrollerer obligatoriske matrikler og autoritativ validering før PostgreSQL-lagring.</summary>
[Collection("Postgres")]
public sealed class MatrikkelValideringTester(PostgresApiOppsett oppsett)
{
    private static OpprettFtbid Prosjekt(string? navn = null) => new()
    {
        Kommunenummer = "0301", Gaardsnummer = 10, Bruksnummer = 1,
        Prosjektnavn = navn ?? "Matrikkelvalidering " + Guid.NewGuid().ToString("N"), System = "Testsystem"
    };

    private static async Task KontrollerFeilAsync(HttpResponseMessage svar, int status, string kode)
    {
        using (svar)
        {
            Assert.Equal(status, (int)svar.StatusCode);
            Assert.Equal("application/problem+json", svar.Content.Headers.ContentType?.MediaType);
            var tekst = await svar.Content.ReadAsStringAsync();
            Assert.DoesNotContain("hemmeligpassord", tekst);
            using var problem = JsonDocument.Parse(tekst);
            Assert.Equal(kode, problem.RootElement.GetProperty("ftbidFeil").GetProperty("Kode").GetString());
        }
    }

    /// <summary>En ugyldig ekstramatrikkel avviser hele prosjektet og bevarer alle fem oppslagsfelter.</summary>
    [Fact]
    public async Task AlleMatriklerMaValideresForOppretting()
    {
        var klient = new Oppslagsklient(m => m.Gaardsnummer == 999 ? [] : [123]);
        using var fabrikk = oppsett.Fabrikk.WithWebHostBuilder(b => b.ConfigureServices(t =>
        { t.RemoveAll<IMatrikkelKlient>(); t.AddSingleton<IMatrikkelKlient>(klient); }));
        using var http = fabrikk.CreateClient();
        using var autorisert = oppsett.Klient();
        http.DefaultRequestHeaders.Authorization = autorisert.DefaultRequestHeaders.Authorization;
        var prosjekt = Prosjekt();
        prosjekt.Matrikkelopplysninger = [new("0301", 999, 2, 3, 4)];
        await KontrollerFeilAsync(await http.PostAsJsonAsync("/api/ftbid", prosjekt), 400, "V0063");
        Assert.Equal([new Matrikkeldata("0301", 10, 1), new Matrikkeldata("0301", 999, 2, 3, 4)], klient.Oppslag.ToArray());
        using var omfang = fabrikk.Services.CreateScope();
        Assert.False(await omfang.ServiceProvider.GetRequiredService<FtbidDbContext>().Ftbider.AnyAsync(f => f.Prosjektnavn == prosjekt.Prosjektnavn));
    }

    /// <summary>En matrikkel uten treff kan ikke legges til en eksisterende ftbid, også i Local.</summary>
    [Theory]
    [InlineData("Test")]
    [InlineData("Local")]
    public async Task NyTilknytningKreverTreffOgsaLokalt(string miljo)
    {
        using var oppretter = oppsett.Klient();
        var ftbid = (await (await oppretter.PostAsJsonAsync("/api/ftbid", Prosjekt())).Content.ReadFromJsonAsync<string>())!;
        var klient = new Oppslagsklient(_ => []);
        using var fabrikk = oppsett.Fabrikk.WithWebHostBuilder(b => b.UseEnvironment(miljo).ConfigureServices(t =>
        { t.RemoveAll<IMatrikkelKlient>(); t.AddSingleton<IMatrikkelKlient>(klient); }));
        using var http = fabrikk.CreateClient();
        http.DefaultRequestHeaders.Authorization = miljo == "Local"
            ? new("Bearer", "lokalt-token") : oppretter.DefaultRequestHeaders.Authorization;
        var matrikkel = new Matrikkeldata("0301", 999, 2, 3, 4);
        await KontrollerFeilAsync(await http.PostAsJsonAsync($"/api/ftbid/{ftbid}/matrikkelopplysninger", matrikkel), 400, "V0063");
        Assert.Equal(matrikkel, Assert.Single(klient.Oppslag));
        var resultat = Assert.Single((await oppretter.GetFromJsonAsync<List<FtbidResultat>>($"/api/ftbid/{ftbid}"))!);
        Assert.Single(resultat.Matrikkelopplysninger);
    }

    /// <summary>Oppslagsfeil gir egne regelkoder og lagrer ingen prosjekt- eller matrikkelrader.</summary>
    [Theory]
    [InlineData("service", 503, "V0064")]
    [InlineData("svar", 502, "V0065")]
    [InlineData("innlogging", 503, "V0066")]
    public async Task FeiletValideringKanIkkeLagreData(string feiltype, int status, string kode)
    {
        using var oppretter = oppsett.Klient();
        var ftbid = (await (await oppretter.PostAsJsonAsync("/api/ftbid", Prosjekt())).Content.ReadFromJsonAsync<string>())!;
        var klient = new Oppslagsklient(_ => throw feiltype switch
        {
            "service" => new MatrikkelOppslagFeiletException(new Exception("hemmeligpassord")),
            "svar" => new UgyldigMatrikkelsvarException(new Exception("hemmeligpassord")),
            _ => new MatrikkelAutentiseringFeiletException(new Exception("hemmeligpassord"))
        });
        using var fabrikk = oppsett.Fabrikk.WithWebHostBuilder(b => b.ConfigureServices(t =>
        { t.RemoveAll<IMatrikkelKlient>(); t.AddSingleton<IMatrikkelKlient>(klient); }));
        using var http = fabrikk.CreateClient();
        http.DefaultRequestHeaders.Authorization = oppretter.DefaultRequestHeaders.Authorization;
        var prosjekt = Prosjekt();
        await KontrollerFeilAsync(await http.PostAsJsonAsync("/api/ftbid", prosjekt), status, kode);
        await KontrollerFeilAsync(await http.PostAsJsonAsync($"/api/ftbid/{ftbid}/matrikkelopplysninger", new Matrikkeldata("0301", 30, 3)), status, kode);
        using var omfang = fabrikk.Services.CreateScope();
        var database = omfang.ServiceProvider.GetRequiredService<FtbidDbContext>();
        Assert.False(await database.Ftbider.AnyAsync(f => f.Prosjektnavn == prosjekt.Prosjektnavn));
        Assert.Single((await database.Ftbider.Include(f => f.Matrikkelopplysninger).SingleAsync(f => f.Ean == ftbid)).Matrikkelopplysninger);
    }

    /// <summary>Et prosjekt uten matrikkel avvises før Kartverket kontaktes eller data lagres.</summary>
    [Fact]
    public async Task MinstEnMatrikkelErObligatorisk()
    {
        var klient = new Oppslagsklient(_ => throw new InvalidOperationException("Skal ikke kontaktes"));
        using var fabrikk = oppsett.Fabrikk.WithWebHostBuilder(b => b.ConfigureServices(t =>
        { t.RemoveAll<IMatrikkelKlient>(); t.AddSingleton<IMatrikkelKlient>(klient); }));
        using var http = fabrikk.CreateClient();
        using var autorisert = oppsett.Klient();
        http.DefaultRequestHeaders.Authorization = autorisert.DefaultRequestHeaders.Authorization;
        await KontrollerFeilAsync(await http.PostAsJsonAsync("/api/ftbid", new OpprettFtbid
        { Prosjektnavn = "Uten matrikkel", System = "Testsystem", Matrikkelopplysninger = [] }), 400, "V0042");
        Assert.Empty(klient.Oppslag);
    }

    private sealed class Oppslagsklient(Func<Matrikkeldata, IReadOnlyList<long>> finn) : IMatrikkelKlient
    {
        internal ConcurrentQueue<Matrikkeldata> Oppslag { get; } = new();
        public Task<IReadOnlyList<long>> HentMatrikkelenhetIderAsync(Matrikkeldata matrikkel, CancellationToken avbrudd = default)
        {
            Oppslag.Enqueue(matrikkel);
            return Task.FromResult(finn(matrikkel));
        }
    }
}
