using System.Net;
using System.Net.Http.Json;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Infrastructure.Persistens;
using Ftpb.Ftbid.Tests.Oppsett;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ftpb.Ftbid.Tests.Integrasjon;

[Collection("Postgres")]
public sealed class TiltakstyperTester(PostgresApiOppsett oppsett)
{
    private static OpprettFtbid NyttProsjekt() => new()
    {
        Prosjektnavn = "Tiltakstyper " + Guid.NewGuid(), Prosjektnr = "P-1", System = "Test",
        Tiltakstyper = ["Tilbygg", "Riving"],
        Eiendom = [new() { Eiendomsidentifikasjon = new("0301", 1, 2) }]
    };

    [Fact]
    public async Task FlereTyperLagresMedHashOgKanSokesHverForSeg()
    {
        using var klient = oppsett.Klient();
        var prosjekt = NyttProsjekt();
        prosjekt.Tiltakstyper.Add(" TILBYGG ");
        var svar = await klient.PostAsJsonAsync("/api/ftbid", prosjekt);
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var ftbid = (await svar.Content.ReadFromJsonAsync<string>())!;
        var resultat = Assert.Single((await klient.GetFromJsonAsync<List<FtbidResultat>>("/api/ftbid/" + ftbid))!);
        Assert.Equal(["Tilbygg", "Riving"], resultat.Tiltakstyper);
        foreach (var type in resultat.Tiltakstyper)
        {
            var treff = await klient.GetFromJsonAsync<List<FtbidResultat>>($"/api/ftbid/sok?ftid={ftbid}&tiltakstype={type.ToUpperInvariant()}");
            Assert.Equal(ftbid, Assert.Single(treff!).Ftbid);
        }
        await using var database = Database();
        var lagret = await database.Ftbider.SingleAsync(f => f.Ean == ftbid);
        Assert.Equal(TiltakstypeSett.LagHash(prosjekt.Tiltakstyper), lagret.TiltakstyperHash);
    }

    [Fact]
    public async Task SamtidigeLikeSettGirEnOpprettingOgEnDublett()
    {
        using var klient = oppsett.Klient();
        var forste = NyttProsjekt(); var andre = NyttProsjekt();
        andre.Prosjektnavn = forste.Prosjektnavn;
        andre.Tiltakstyper = [" RIVING ", "tilbygg", "TILBYGG"];
        var svar = await Task.WhenAll(klient.PostAsJsonAsync("/api/ftbid", forste), klient.PostAsJsonAsync("/api/ftbid", andre));
        Assert.Single(svar, s => s.StatusCode == HttpStatusCode.OK);
        Assert.Single(svar, s => s.StatusCode == HttpStatusCode.Conflict);
        forste.Tiltakstyper = ["Tilbygg"];
        Assert.Equal(HttpStatusCode.OK, (await klient.PostAsJsonAsync("/api/ftbid", forste)).StatusCode);
    }

    [Fact]
    public async Task EldrePosterUtenHashDeltarIDublettkontrollen()
    {
        using var klient = oppsett.Klient();
        var prosjekt = NyttProsjekt();
        var svar = await klient.PostAsJsonAsync("/api/ftbid", prosjekt);
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var ftbid = (await svar.Content.ReadFromJsonAsync<string>())!;
        await using var database = Database();
        await database.Ftbider.Where(f => f.Ean == ftbid).ExecuteUpdateAsync(s => s.SetProperty(f => f.TiltakstyperHash, (string?)null));
        prosjekt.Tiltakstyper.Reverse();
        Assert.Equal(HttpStatusCode.Conflict, (await klient.PostAsJsonAsync("/api/ftbid", prosjekt)).StatusCode);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("tom")]
    [InlineData("null-element")]
    [InlineData("tom-verdi")]
    [InlineData("mellomrom")]
    [InlineData("for-lang")]
    public async Task UgyldigeListerAvvises(string variant)
    {
        using var klient = oppsett.Klient();
        var prosjekt = NyttProsjekt();
        prosjekt.Tiltakstyper = variant switch
        {
            "null" => null!, "tom" => [], "null-element" => ["Tilbygg", null!],
            "tom-verdi" => ["Tilbygg", ""], "mellomrom" => ["Tilbygg", " \t "],
            _ => [new string('a', 201)]
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await klient.PostAsJsonAsync("/api/ftbid", prosjekt)).StatusCode);
    }

    private FtbidDbContext Database() => new(new DbContextOptionsBuilder<FtbidDbContext>().UseNpgsql(oppsett.Tilkobling).Options);
}
