using System.Net;
using System.Net.Http.Json;
using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Tests.Fixtures;
using Xunit;

namespace Ftpb.Ftbid.Tests.Integration;

/// <summary>Tester hele eiendommens livssyklus og eierskap, uten eksterne eiendomsoppslag.</summary>
[Collection("Postgres")]
public sealed class FtbidEiendomTests(PostgresApiFixture fixture)
{
    private static Eiendomsdata Eiendom(int gard = 124) => new() { Eiendomsidentifikasjon = new("0301", gard, 55, 2),
        Adresse = [new("Admiralveien 2A", "Hyttefelt 1", Postnr: "0318", Poststed: "Oslo", Landkode: "NO",
            Gatenavn: "Admiralveien", Husnr: "2", Bokstav: "A")], Bygningsnummer = "300827824", Bolignummer = "H0201", Kommunenavn = "Oslo" };

    private static async Task<string> CreateAsync(HttpClient client) =>
        (await (await client.PostAsJsonAsync("/api/ftbid", new OpprettFtbid { Prosjektnavn = Guid.NewGuid().ToString(), System = "Test", Prosjektnr = "P-1", Tiltakstyper = ["Tilbygg"], Eiendom = [Eiendom()] }))
            .Content.ReadFromJsonAsync<string>())!;

    [Fact]
    public async Task NullEiendommerTilleggErstatningOgSlettingAvSisteFungerer()
    {
        using var client = fixture.CreateClient();
        var ftbid = await CreateAsync(client);
        var opprinnelig = Assert.Single((await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid/" + ftbid))!);
        await client.DeleteAsync($"/api/ftbid/{ftbid}/eiendom/{Assert.Single(opprinnelig.Eiendom).Id}");
        async Task<FtbidResult> Hent() => Assert.Single((await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid/" + ftbid))!);
        Assert.Empty((await Hent()).Eiendom);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/ftbid/{ftbid}/eiendom", Eiendom())).StatusCode);
        var lagret = Assert.Single((await Hent()).Eiendom);
        Assert.Equal("0318", Assert.Single(lagret.Adresse).Postnr);
        var endret = Eiendom(999);
        endret.Adresse = [new("Ny adresse 1"), new("Ny adresse 2")];
        var sti = $"/api/ftbid/{ftbid}/eiendom/{lagret.Id}";
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(sti, endret)).StatusCode);
        var ny = Assert.Single((await Hent()).Eiendom);
        Assert.Equal(lagret.Id, ny.Id);
        Assert.Equal(999, ny.Eiendomsidentifikasjon.Gaardsnummer);
        Assert.Equal(2, ny.Adresse.Count);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(sti)).StatusCode);
        Assert.Empty((await Hent()).Eiendom);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(sti)).StatusCode);
    }

    [Fact]
    public async Task EiendomsIdKanIkkeBrukesPaEnAnnenFtbid()
    {
        using var client = fixture.CreateClient();
        var forste = await CreateAsync(client);
        var andre = await CreateAsync(client);
        var data = Assert.Single((await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid/" + forste))!);
        var id = Assert.Single(data.Eiendom).Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/ftbid/{andre}/eiendom/{id}", Eiendom())).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/ftbid/{andre}/eiendom/{id}")).StatusCode);
        Assert.Single(Assert.Single((await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid/" + forste))!).Eiendom);
    }

    [Fact]
    public async Task SamtidigeTilleggBevarerAlleEiendommerOgKreverEndrescope()
    {
        using var client = fixture.CreateClient();
        var ftbid = await CreateAsync(client);
        var response = await Task.WhenAll(Enumerable.Range(1, 8).Select(i => client.PostAsJsonAsync($"/api/ftbid/{ftbid}/eiendom", Eiendom(i))));
        Assert.All(response, s => Assert.Equal(HttpStatusCode.NoContent, s.StatusCode));
        var result = Assert.Single((await client.GetFromJsonAsync<List<FtbidResult>>("/api/ftbid/" + ftbid))!);
        Assert.Equal(9, result.Eiendom.Count);
        using var leser = fixture.CreateClient("dibk:bygg/ftbid.les");
        Assert.Equal(HttpStatusCode.Forbidden, (await leser.PostAsJsonAsync($"/api/ftbid/{ftbid}/eiendom", Eiendom())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await leser.PutAsJsonAsync($"/api/ftbid/{ftbid}/eiendom/{result.Eiendom[0].Id}", Eiendom())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await leser.DeleteAsync($"/api/ftbid/{ftbid}/eiendom/{result.Eiendom[0].Id}")).StatusCode);
    }
}
