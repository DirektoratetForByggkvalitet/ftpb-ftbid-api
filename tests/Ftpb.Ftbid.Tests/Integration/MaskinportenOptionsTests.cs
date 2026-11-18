using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Domain.Referansedata;
using Ftpb.Ftbid.Infrastructure.Persistence;
using Ftpb.Ftbid.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ftpb.Ftbid.Tests.Integration;

/// <summary>Verifiserer at konfigurasjonsflagget styrer tilgang til alle ftbid-endepunkter.</summary>
[Collection("Postgres")]
public sealed class MaskinportenOptionsTests(PostgresApiFixture fixture)
{
    /// <summary>Alle endepunkter krever token når aktivert og fungerer uten token når deaktivert.</summary>
    [Theory]
    [InlineData("Test", true)]
    [InlineData("Test", false)]
    [InlineData("Local", true)]
    [InlineData("Local", false)]
    [InlineData("Development", true)]
    [InlineData("Production", true)]
    public async Task FlaggetStyrerTokenkravOgSwagger(string environment, bool enabled)
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment(environment).UseSetting("Maskinporten:Aktivert", enabled.ToString()));
        using var client = factory.CreateClient();
        var arkivreferanse = "urn:dibk:bygg:flagg:" + Guid.NewGuid().ToString("N");
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FtbidDbContext>();
            database.Skjemametadata.Add(new Skjemametadata { Arkivreferanse = arkivreferanse });
            await database.SaveChangesAsync();
        }
        using var oppretting = await client.PostAsJsonAsync("/api/ftbid", new OpprettFtbid
        {
            Prosjektnavn = "Flaggtest " + Guid.NewGuid().ToString("N"), System = "Testsystem",
            Prosjektnr = "P-1", Tiltakstyper = ["Tilbygg"],
            Eiendom = [new() { Eiendomsidentifikasjon = new("0301", 1, 2) }]
        });
        Assert.Equal(enabled ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, oppretting.StatusCode);
        var ftbid = enabled ? "709999900000000017" : (await oppretting.Content.ReadFromJsonAsync<string>())!;
        using var tilknytning = await client.PostAsJsonAsync($"/api/ftbid/{ftbid}/tilknytninger", new TilknyttFtbidRequest
        { Tilknytninger = [new() { ReferanseId = arkivreferanse }] });
        Assert.Equal(enabled ? HttpStatusCode.Unauthorized : HttpStatusCode.NoContent, tilknytning.StatusCode);
        using var oppslag = await client.GetAsync("/api/ftbid/" + ftbid);
        using var sok = await client.GetAsync("/api/ftbid?kommunenummer=0301");
        Assert.Equal(enabled ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, oppslag.StatusCode);
        Assert.Equal(enabled ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, sok.StatusCode);
        if (!enabled)
        {
            var prosjekt = Assert.Single((await oppslag.Content.ReadFromJsonAsync<List<FtbidResult>>())!);
            Assert.Equal(arkivreferanse, Assert.Single(prosjekt.Tilknytninger).ReferanseId);
        }
        if (environment == "Production")
        {
            using var openapi = await client.GetAsync("/openapi/v1.json");
            Assert.Equal(HttpStatusCode.NotFound, openapi.StatusCode);
            return;
        }
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var harOrdninger = document.RootElement.GetProperty("components").TryGetProperty("securitySchemes", out var ordninger)
            && ordninger.TryGetProperty("Maskinporten", out _);
        Assert.Equal(enabled, harOrdninger);
        var harKrav = document.RootElement.TryGetProperty("security", out var krav) && krav.GetArrayLength() > 0;
        Assert.Equal(enabled, harKrav);
    }
}
