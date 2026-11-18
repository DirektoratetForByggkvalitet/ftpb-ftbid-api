using System.Net;
using System.Net.Http.Json;
using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Infrastructure.Persistence;
using Ftpb.Ftbid.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ftpb.Ftbid.Tests.Integration;

[Collection("Postgres")]
public sealed class TiltakstyperTests(PostgresApiFixture fixture)
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
        using var client = fixture.CreateClient();
        var prosjekt = NyttProsjekt();
        prosjekt.Tiltakstyper.Add(" TILBYGG ");
        var response = await client.PostAsJsonAsync("/api/ftbid", prosjekt);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ftbid = (await response.Content.ReadFromJsonAsync<string>())!;
        var result = Assert.Single((await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid/" + ftbid))!);
        Assert.Equal(["Tilbygg", "Riving"], result.Tiltakstyper);
        foreach (var type in result.Tiltakstyper)
        {
            var treff = await client.GetFromJsonAsync<List<FtbidResult>>($"/api/ftbid?ftid={ftbid}&tiltakstype={type.ToUpperInvariant()}");
            Assert.Equal(ftbid, Assert.Single(treff!).Ftbid);
        }
        await using var database = Database();
        var lagret = await database.Ftbider.SingleAsync(f => f.Ean == ftbid);
        Assert.Equal(TiltakstypeSett.ComputeHash(prosjekt.Tiltakstyper), lagret.TiltakstyperHash);
    }

    [Fact]
    public async Task SamtidigeLikeSettGirEnOpprettingOgEnDublett()
    {
        using var client = fixture.CreateClient();
        var forste = NyttProsjekt(); var andre = NyttProsjekt();
        andre.Prosjektnavn = forste.Prosjektnavn;
        andre.Tiltakstyper = [" RIVING ", "tilbygg", "TILBYGG"];
        var response = await Task.WhenAll(client.PostAsJsonAsync("/api/ftbid", forste), client.PostAsJsonAsync("/api/ftbid", andre));
        Assert.Single(response, s => s.StatusCode == HttpStatusCode.OK);
        Assert.Single(response, s => s.StatusCode == HttpStatusCode.Conflict);
        forste.Tiltakstyper = ["Tilbygg"];
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/ftbid", forste)).StatusCode);
    }

    [Fact]
    public async Task EldrePosterUtenHashDeltarIDublettkontrollen()
    {
        using var client = fixture.CreateClient();
        var prosjekt = NyttProsjekt();
        var response = await client.PostAsJsonAsync("/api/ftbid", prosjekt);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ftbid = (await response.Content.ReadFromJsonAsync<string>())!;
        await using var database = Database();
        await database.Ftbider.Where(f => f.Ean == ftbid).ExecuteUpdateAsync(s => s.SetProperty(f => f.TiltakstyperHash, (string?)null));
        prosjekt.Tiltakstyper.Reverse();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/ftbid", prosjekt)).StatusCode);
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
        using var client = fixture.CreateClient();
        var prosjekt = NyttProsjekt();
        prosjekt.Tiltakstyper = variant switch
        {
            "null" => null!, "tom" => [], "null-element" => ["Tilbygg", null!],
            "tom-verdi" => ["Tilbygg", ""], "mellomrom" => ["Tilbygg", " \t "],
            _ => [new string('a', 201)]
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/ftbid", prosjekt)).StatusCode);
    }

    private FtbidDbContext Database() => new(new DbContextOptionsBuilder<FtbidDbContext>().UseNpgsql(fixture.ConnectionString).Options);
}
