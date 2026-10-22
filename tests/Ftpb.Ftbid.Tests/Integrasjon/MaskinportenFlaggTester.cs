using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Domain.Referansedata;
using Ftpb.Ftbid.Infrastructure.Persistens;
using Ftpb.Ftbid.Tests.Oppsett;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ftpb.Ftbid.Tests.Integrasjon;

/// <summary>Verifiserer at konfigurasjonsflagget styrer tilgang til alle ftbid-endepunkter.</summary>
[Collection("Postgres")]
public sealed class MaskinportenFlaggTester(PostgresApiOppsett oppsett)
{
    /// <summary>Alle endepunkter krever token når aktivert og fungerer uten token når deaktivert.</summary>
    [Theory]
    [InlineData("Test", true)]
    [InlineData("Test", false)]
    [InlineData("Local", true)]
    [InlineData("Local", false)]
    [InlineData("Development", true)]
    [InlineData("Production", true)]
    public async Task FlaggetStyrerTokenkravOgSwagger(string miljo, bool aktivert)
    {
        using var fabrikk = oppsett.Fabrikk.WithWebHostBuilder(bygger =>
            bygger.UseEnvironment(miljo).UseSetting("Maskinporten:Aktivert", aktivert.ToString()));
        using var klient = fabrikk.CreateClient();
        var arkivreferanse = "ARFLAGG" + Guid.NewGuid().ToString("N");
        using (var omfang = fabrikk.Services.CreateScope())
        {
            var database = omfang.ServiceProvider.GetRequiredService<FtbidDbContext>();
            database.Skjemametadata.Add(new Skjemametadata { Arkivreferanse = arkivreferanse });
            await database.SaveChangesAsync();
        }
        using var oppretting = await klient.PostAsJsonAsync("/api/ftbid", new OpprettFtbid
        {
            Prosjektnavn = "Flaggtest " + Guid.NewGuid().ToString("N"), System = "Testsystem",
            Prosjektnr = "P-1", Tiltakstyper = ["Tilbygg"],
            Eiendom = [new() { Eiendomsidentifikasjon = new("0301", 1, 2) }]
        });
        Assert.Equal(aktivert ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, oppretting.StatusCode);
        var ftbid = aktivert ? "709999900000000017" : (await oppretting.Content.ReadFromJsonAsync<string>())!;
        using var tilknytning = await klient.PostAsJsonAsync("/api/ftbid/tilknytt", new TilknyttFtbid
        { Ftbid = ftbid, Arkivreferanse = arkivreferanse });
        Assert.Equal(aktivert ? HttpStatusCode.Unauthorized : HttpStatusCode.NoContent, tilknytning.StatusCode);
        using var oppslag = await klient.GetAsync("/api/ftbid/" + ftbid);
        using var sok = await klient.GetAsync("/api/ftbid/sok?kommunenummer=0301");
        Assert.Equal(aktivert ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, oppslag.StatusCode);
        Assert.Equal(aktivert ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, sok.StatusCode);
        if (!aktivert)
        {
            var prosjekt = Assert.Single((await oppslag.Content.ReadFromJsonAsync<List<FtbidResultat>>())!);
            Assert.Equal(arkivreferanse, Assert.Single(prosjekt.TilknyttedeSkjemaer).Arkivreferanse);
        }
        if (miljo == "Production")
        {
            using var openapi = await klient.GetAsync("/openapi/v1.json");
            Assert.Equal(HttpStatusCode.NotFound, openapi.StatusCode);
            return;
        }
        using var dokument = JsonDocument.Parse(await klient.GetStringAsync("/openapi/v1.json"));
        var harOrdninger = dokument.RootElement.GetProperty("components").TryGetProperty("securitySchemes", out var ordninger)
            && ordninger.TryGetProperty("Maskinporten", out _);
        Assert.Equal(aktivert, harOrdninger);
        var harKrav = dokument.RootElement.TryGetProperty("security", out var krav) && krav.GetArrayLength() > 0;
        Assert.Equal(aktivert, harKrav);
    }
}
