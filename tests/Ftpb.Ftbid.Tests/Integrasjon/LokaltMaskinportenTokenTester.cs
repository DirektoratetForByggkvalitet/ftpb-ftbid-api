using System.Net;
using Ftpb.Ftbid.Tests.Oppsett;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Ftpb.Ftbid.Tests.Integrasjon;

/// <summary>Kontrollerer skillet mellom tilstedeværelse lokalt og full validering i øvrige miljøer.</summary>
[Collection("Postgres")]
public sealed class LokaltMaskinportenTokenTester(PostgresApiOppsett oppsett)
{
    /// <summary>Local avviser tomme og feilformede headere, men krever ikke gyldig JWT-innhold.</summary>
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("", HttpStatusCode.Unauthorized)]
    [InlineData("Bearer", HttpStatusCode.Unauthorized)]
    [InlineData("Bearer   ", HttpStatusCode.Unauthorized)]
    [InlineData("Basic lokalt-token", HttpStatusCode.Unauthorized)]
    [InlineData("Bearer to verdier", HttpStatusCode.Unauthorized)]
    [InlineData("Bearer lokalt-token", HttpStatusCode.OK)]
    public async Task LocalKreverKunEnIkkeTomBearerVerdi(string? header, HttpStatusCode forventet)
    {
        using var fabrikk = oppsett.Fabrikk.WithWebHostBuilder(bygger => bygger.UseEnvironment("Local"));
        using var klient = fabrikk.CreateClient();
        if (header is not null) klient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", header);
        using var svar = await klient.GetAsync("/api/ftbid/99999999");
        Assert.Equal(forventet, svar.StatusCode);
        if (forventet == HttpStatusCode.Unauthorized)
            Assert.Equal("application/problem+json", svar.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>Ugyldige JWT-er aksepteres bare i Local; full validering beholdes utenfor Local.</summary>
    [Theory]
    [InlineData("Local", "utlopt", HttpStatusCode.OK)]
    [InlineData("Local", "feil_signatur", HttpStatusCode.OK)]
    [InlineData("Local", "feil_utsteder", HttpStatusCode.OK)]
    [InlineData("Local", "uten_konsument", HttpStatusCode.OK)]
    [InlineData("Development", "utlopt", HttpStatusCode.Unauthorized)]
    [InlineData("Development", "feil_signatur", HttpStatusCode.Unauthorized)]
    [InlineData("Test", "feil_utsteder", HttpStatusCode.Unauthorized)]
    [InlineData("Production", "utlopt", HttpStatusCode.Unauthorized)]
    public async Task LokalForenklingGjelderBareLocal(string miljo, string variant, HttpStatusCode forventet)
    {
        using var fabrikk = oppsett.Fabrikk.WithWebHostBuilder(bygger => bygger.UseEnvironment(miljo));
        using var klient = fabrikk.CreateClient();
        using var tokenklient = oppsett.Klient(scope: "", variant: variant);
        klient.DefaultRequestHeaders.Authorization = tokenklient.DefaultRequestHeaders.Authorization;
        using var svar = await klient.GetAsync("/api/ftbid/99999999");
        Assert.Equal(forventet, svar.StatusCode);
    }
}
