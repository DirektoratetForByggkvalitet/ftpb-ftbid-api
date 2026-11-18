using System.Net;
using Ftpb.Ftbid.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Ftpb.Ftbid.Tests.Integration;

/// <summary>Kontrollerer skillet mellom tilstedeværelse i Local/Development og full validering i Test/Production.</summary>
[Collection("Postgres")]
public sealed class LocalMaskinportenAuthenticationTests(PostgresApiFixture fixture)
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
    public async Task UtviklingsmiljoerKreverKunEnIkkeTomBearerVerdi(string environment, string? header, HttpStatusCode expected)
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));
        using var client = factory.CreateClient();
        if (header is not null) client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", header);
        using var response = await client.GetAsync("/api/ftbid/99999999");
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.Unauthorized)
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
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
    [InlineData("Test", "feil_signatur", HttpStatusCode.Unauthorized)]
    [InlineData("Test", "utlopt", HttpStatusCode.Unauthorized)]
    [InlineData("Test", "uten_konsument", HttpStatusCode.Unauthorized)]
    [InlineData("Production", "utlopt", HttpStatusCode.Unauthorized)]
    public async Task ForenklingGjelderBareLocalOgDevelopment(string environment, string variant, HttpStatusCode expected)
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));
        using var client = factory.CreateClient();
        using var tokenklient = fixture.CreateClient(scope: "", variant: variant);
        client.DefaultRequestHeaders.Authorization = tokenklient.DefaultRequestHeaders.Authorization;
        using var response = await client.GetAsync("/api/ftbid/99999999");
        Assert.Equal(expected, response.StatusCode);
    }
}
