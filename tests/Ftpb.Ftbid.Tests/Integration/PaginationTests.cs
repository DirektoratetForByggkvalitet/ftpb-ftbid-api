using System.Net;
using System.Net.Http.Json;
using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Domain.Identifikatorer;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Infrastructure.Persistence;
using Ftpb.Ftbid.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Prosjekt = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Tests.Integration;

[Collection("Postgres")]
public sealed class PaginationTests(PostgresApiFixture fixture)
{
    [Fact]
    public async Task ListerAllePosterMedStabileSidegrenserOgMetadata()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<FtbidDbContext>();
        var prosjekter = Enumerable.Range(0, 51).Select(i => new Prosjekt
        {
            Ean = EanGenerator.Generate("7099999", 9_000_000 + i),
            Utstedt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero).AddMinutes(i),
            Prosjektnavn = "Pagination " + i,
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
            using var client = fixture.CreateClient("dibk:bygg/ftbid.les");
            var forventetFtbid = await database.Ftbider.OrderBy(f => f.Ean).Select(f => f.Ean).ToListAsync();
            var ftbider = await HentAlleAsync<FtbidResult>(client, "/api/ftbid", forventetFtbid.Count);
            Assert.Equal(forventetFtbid, ftbider.Select(f => f.Ftbid));
            foreach (var expected in prosjekter)
                Assert.Equal(expected.Utstedt, Assert.Single(ftbider, f => f.Ftbid == expected.Ean).Utstedt);
            var prosjekt = Assert.Single(ftbider, f => f.Ftbid == prosjekter[0].Ean);
            Assert.Equal(2, prosjekt.Eiendom.Count);
            Assert.Equal(new[] { "Første", "Andre" }, prosjekt.Eiendom[0].Adresse.Select(a => a.Adresselinje1));

            var forventetEiendom = await database.Set<Eiendom>().OrderBy(e => e.Id).Select(e => e.Id).ToListAsync();
            var eiendommer = await HentAlleAsync<EiendomListResult>(client, "/api/ftbid/eiendom", forventetEiendom.Count);
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

    private static async Task<List<T>> HentAlleAsync<T>(HttpClient client, string sti, int count)
    {
        var alle = new List<T>();
        var antallSider = (count + 49) / 50;
        for (var side = 1; side <= antallSider + 1; side++)
        {
            // Første side bruker standardverdien når side ikke er angitt.
            var response = (await client.GetFromJsonAsync<PagedResult<T>>(side == 1 ? sti : $"{sti}?side={side}"))!;
            Assert.Equal(side, response.Side);
            Assert.Equal(50, response.Sidestorrelse);
            Assert.Equal(Math.Clamp(count - (side - 1) * 50, 0, 50), response.Resultater.Count);
            Assert.Equal(side < antallSider, response.HarNesteSide);
            alle.AddRange(response.Resultater);
        }
        return alle;
    }

    [Theory]
    [InlineData("prosjektnavn=%20&kommunenummer=")]
    [InlineData("ftid=%20&tiltakstype=%20&bolignummer=%20")]
    public async Task TommeFiltreGirListeMensUtfylteFiltreBeholderSokeformatet(string filters)
    {
        using var client = fixture.CreateClient("dibk:bygg/ftbid.les");
        var liste = await client.GetFromJsonAsync<PagedResult<FtbidResult>>("/api/ftbid?" + filters + "&side=1");
        Assert.NotNull(liste);
        Assert.Equal(1, liste.Side);
        Assert.Equal(50, liste.Sidestorrelse);
        // Et ugyldig numerisk filter skal fortsatt valideres som et søk.
        var response = await client.GetAsync("/api/ftbid?gaardsnummer=-1");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // Ingen treff med et utfylt filter beholder søkets 404-respons.
        response = await client.GetAsync("/api/ftbid?prosjektnavn=" + Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/ftbid")]
    [InlineData("/api/ftbid/eiendom")]
    public async Task ValidererSidetalOgKreverLesescope(string sti)
    {
        using var leser = fixture.CreateClient("dibk:bygg/ftbid.les");
        foreach (var side in new[] { "0", "-1", "abc", "42949674", "2147483647", "2147483648" })
        {
            var response = await leser.GetAsync($"{sti}?side={side}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
        Assert.Equal(HttpStatusCode.OK, (await leser.GetAsync($"{sti}?side=42949673")).StatusCode);
        using var skriver = fixture.CreateClient("dibk:bygg/ftbid.endre");
        Assert.Equal(HttpStatusCode.Forbidden, (await skriver.GetAsync(sti)).StatusCode);
        using var anonym = fixture.CreateClient(variant: "mangler");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonym.GetAsync(sti)).StatusCode);
    }
}
