using System.Net;
using System.Net.Http.Json;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Domain.Identifikatorer;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Infrastructure.Persistens;
using Ftpb.Ftbid.Tests.Oppsett;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Prosjekt = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Tests.Integrasjon;

[Collection("Postgres")]
public sealed class PagineringTester(PostgresApiOppsett oppsett)
{
    [Fact]
    public async Task ListerAllePosterMedStabileSidegrenserOgMetadata()
    {
        using var omfang = oppsett.Fabrikk.Services.CreateScope();
        var database = omfang.ServiceProvider.GetRequiredService<FtbidDbContext>();
        var prosjekter = Enumerable.Range(0, 51).Select(i => new Prosjekt
        {
            Ean = EanGenerator.Lag("7099999", 9_000_000 + i),
            Prosjektnavn = "Paginering " + i,
            NormalisertProsjektnavn = "paginering " + i,
            Eiendom = Enumerable.Range(0, 2).Select(j => new Eiendom
            {
                Rekkefolge = j,
                Eiendomsidentifikasjon = new("0301", i, j),
                Adresse = [new() { Rekkefolge = 1, Adresselinje1 = "Andre" },
                    new() { Rekkefolge = 0, Adresselinje1 = "Første" }]
            }).ToList()
        }).ToList();
        database.Ftbider.AddRange(prosjekter);
        await database.SaveChangesAsync();
        try
        {
            using var klient = oppsett.Klient("dibk:bygg/ftbid.les");
            var forventetFtbid = await database.Ftbider.OrderBy(f => f.Ean).Select(f => f.Ean).ToListAsync();
            var ftbider = await HentAlleAsync<FtbidResultat>(klient, "/api/ftbid", forventetFtbid.Count);
            Assert.Equal(forventetFtbid, ftbider.Select(f => f.Ftbid));
            var prosjekt = Assert.Single(ftbider, f => f.Ftbid == prosjekter[0].Ean);
            Assert.Equal(2, prosjekt.Eiendom.Count);
            Assert.Equal(new[] { "Første", "Andre" }, prosjekt.Eiendom[0].Adresse.Select(a => a.Adresselinje1));

            var forventetEiendom = await database.Set<Eiendom>().OrderBy(e => e.Id).Select(e => e.Id).ToListAsync();
            var eiendommer = await HentAlleAsync<EiendomListeResultat>(klient, "/api/ftbid/eiendom", forventetEiendom.Count);
            Assert.Equal(forventetEiendom, eiendommer.Select(e => e.Eiendom.Id));
            var eiendom = Assert.Single(eiendommer, e => e.Eiendom.Id == prosjekter[0].Eiendom[0].Id);
            Assert.Equal(prosjekter[0].Ean, eiendom.Ftbid);
            Assert.Equal(new[] { "Første", "Andre" }, eiendom.Eiendom.Adresse.Select(a => a.Adresselinje1));
        }
        finally
        {
            database.Ftbider.RemoveRange(prosjekter);
            await database.SaveChangesAsync();
        }
    }

    private static async Task<List<T>> HentAlleAsync<T>(HttpClient klient, string sti, int antall)
    {
        var alle = new List<T>();
        var antallSider = (antall + 49) / 50;
        for (var side = 1; side <= antallSider + 1; side++)
        {
            // Første side bruker standardverdien når side ikke er angitt.
            var svar = (await klient.GetFromJsonAsync<PaginertResultat<T>>(side == 1 ? sti : $"{sti}?side={side}"))!;
            Assert.Equal(side, svar.Side);
            Assert.Equal(50, svar.Sidestorrelse);
            Assert.Equal(Math.Clamp(antall - (side - 1) * 50, 0, 50), svar.Resultater.Count);
            Assert.Equal(side < antallSider, svar.HarNesteSide);
            alle.AddRange(svar.Resultater);
        }
        return alle;
    }

    [Theory]
    [InlineData("prosjektnavn=%20&kommunenummer=")]
    [InlineData("ftid=%20&tiltakstype=%20&bolignummer=%20")]
    public async Task TommeFiltreGirListeMensUtfylteFiltreBeholderSokeformatet(string filtre)
    {
        using var klient = oppsett.Klient("dibk:bygg/ftbid.les");
        var liste = await klient.GetFromJsonAsync<PaginertResultat<FtbidResultat>>("/api/ftbid?" + filtre + "&side=1");
        Assert.NotNull(liste);
        Assert.Equal(1, liste.Side);
        Assert.Equal(50, liste.Sidestorrelse);
        // Et ugyldig numerisk filter skal fortsatt valideres som et søk.
        var svar = await klient.GetAsync("/api/ftbid?gaardsnummer=-1");
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
        // Ingen treff med et utfylt filter beholder søkets 404-respons.
        svar = await klient.GetAsync("/api/ftbid?prosjektnavn=" + Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Theory]
    [InlineData("/api/ftbid")]
    [InlineData("/api/ftbid/eiendom")]
    public async Task ValidererSidetalOgKreverLesescope(string sti)
    {
        using var leser = oppsett.Klient("dibk:bygg/ftbid.les");
        foreach (var side in new[] { "0", "-1", "abc", "42949674", "2147483647", "2147483648" })
        {
            var svar = await leser.GetAsync($"{sti}?side={side}");
            Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
            Assert.Equal("application/problem+json", svar.Content.Headers.ContentType?.MediaType);
        }
        Assert.Equal(HttpStatusCode.OK, (await leser.GetAsync($"{sti}?side=42949673")).StatusCode);
        using var skriver = oppsett.Klient("dibk:bygg/ftbid.endre");
        Assert.Equal(HttpStatusCode.Forbidden, (await skriver.GetAsync(sti)).StatusCode);
        using var anonym = oppsett.Klient(variant: "mangler");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonym.GetAsync(sti)).StatusCode);
    }
}
