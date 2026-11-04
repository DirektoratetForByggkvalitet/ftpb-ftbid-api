using System.Net;
using Ftpb.Ftbid.Tests.Oppsett;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Ftpb.Ftbid.Tests.Integrasjon;

/// <summary>Kontrollerer skillet mellom tilstedeværelse i Local/Development og full validering i Test/Production.</summary>
[Collection("Postgres")]
public sealed class LokaltMaskinportenTokenTester(PostgresApiOppsett oppsett)
{
    /// <summary>Local og Development avviser tomme og feilformede headere, men krever ikke gyldig JWT-innhold.</summary>
    [Theory]
    [InlineData("Local", null, HttpStatusCode.Unauthorized)]
    [InlineData("Local", "", HttpStatusCode.Unauthorized)]
    [InlineData("Local", "Bearer", HttpStatusCode.Unauthorized)]
    [InlineData("Local", "Bearer   ", HttpStatusCode.Unauthorized)]
    [InlineData("Local", "Basic lokalt-token", HttpStatusCode.Unauthorized)]
    [InlineData("Local", "Bearer to verdier", HttpStatusCode.Unauthorized)]
    [InlineData("Local", "Bearer lokalt-token", HttpStatusCode.OK)]
    [InlineData("Development", null, HttpStatusCode.Unauthorized)]
    [InlineData("Development", "", HttpStatusCode.Unauthorized)]
    [InlineData("Development", "Bearer", HttpStatusCode.Unauthorized)]
    [InlineData("Development", "Bearer   ", HttpStatusCode.Unauthorized)]
    [InlineData("Development", "Basic lokalt-token", HttpStatusCode.Unauthorized)]
    [InlineData("Development", "Bearer to verdier", HttpStatusCode.Unauthorized)]
    [InlineData("Development", "Bearer lokalt-token", HttpStatusCode.OK)]
    public async Task UtviklingsmiljoerKreverKunEnIkkeTomBearerVerdi(string miljo, string? header, HttpStatusCode forventet)
    {
        using var fabrikk = oppsett.Fabrikk.WithWebHostBuilder(bygger => bygger.UseEnvironment(miljo));
        using var klient = fabrikk.CreateClient();
        if (header is not null) klient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", header);
        using var svar = await klient.GetAsync("/api/ftbid/99999999");
        Assert.Equal(forventet, svar.StatusCode);
        if (forventet == HttpStatusCode.Unauthorized)
            Assert.Equal("application/problem+json", svar.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>Ugyldige JWT-er aksepteres i Local og Development; Test og Production validerer fortsatt tokeninnholdet.</summary>
    [Theory]
    [InlineData("Local", "utlopt", HttpStatusCode.OK)]
    [InlineData("Local", "feil_signatur", HttpStatusCode.OK)]
    [InlineData("Local", "feil_utsteder", HttpStatusCode.OK)]
    [InlineData("Local", "uten_konsument", HttpStatusCode.OK)]
    [InlineData("Development", "utlopt", HttpStatusCode.OK)]
    [InlineData("Development", "feil_signatur", HttpStatusCode.OK)]
    [InlineData("Development", "feil_utsteder", HttpStatusCode.OK)]
    [InlineData("Development", "uten_konsument", HttpStatusCode.OK)]
    [InlineData("Test", "feil_utsteder", HttpStatusCode.Unauthorized)]
    [InlineData("Production", "utlopt", HttpStatusCode.Unauthorized)]
    public async Task ForenklingGjelderBareLocalOgDevelopment(string miljo, string variant, HttpStatusCode forventet)
    {
        using var fabrikk = oppsett.Fabrikk.WithWebHostBuilder(bygger => bygger.UseEnvironment(miljo));
        using var klient = fabrikk.CreateClient();
        using var tokenklient = oppsett.Klient(scope: "", variant: variant);
        klient.DefaultRequestHeaders.Authorization = tokenklient.DefaultRequestHeaders.Authorization;
        using var svar = await klient.GetAsync("/api/ftbid/99999999");
        Assert.Equal(forventet, svar.StatusCode);
    }
}
