using Ftpb.Ftbid.Api.Authentication;
using Ftpb.Ftbid.Api.BackgroundServices;
using Ftpb.Ftbid.Infrastructure.Matrikkel;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ftpb.Ftbid.Tests.Unit;

public sealed class ConfigurationBindingTests
{
    [Fact]
    public void EnglishOptionsBindExistingNorwegianConfigurationKeys()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Maskinporten:Aktivert"] = "false",
            ["Maskinporten:Utsteder"] = "https://issuer.example/",
            ["Maskinporten:Publikum"] = "ftbid-api",
            ["Maskinporten:LesScope"] = "test:les",
            ["Maskinporten:EndreScope"] = "test:endre",
            ["Matrikkel:BasisUrl"] = "https://matrikkel.example/",
            ["Matrikkel:Brukernavn"] = "test-user",
            ["Matrikkel:Passord"] = "test-password",
            ["Matrikkel:TidsavbruddSekunder"] = "42",
            ["Adresseberikelse:Aktivert"] = "true",
            ["Adresseberikelse:Batchstorrelse"] = "17",
            ["Adresseberikelse:PauseMellomOppslagMs"] = "123",
            ["Adresseberikelse:IntervallMinutter"] = "23"
        }).Build();

        var authentication = configuration.GetSection("Maskinporten").Get<MaskinportenOptions>()!;
        Assert.False(authentication.Enabled);
        Assert.Equal("https://issuer.example/", authentication.Issuer);
        Assert.Equal("ftbid-api", authentication.Audience);
        Assert.Equal("test:les", authentication.ReadScope);
        Assert.Equal("test:endre", authentication.WriteScope);

        var matrikkel = configuration.GetSection("Matrikkel").Get<MatrikkelOptions>()!;
        Assert.Equal("https://matrikkel.example/", matrikkel.BaseUrl);
        Assert.Equal("test-user", matrikkel.Username);
        Assert.Equal("test-password", matrikkel.Password);
        Assert.Equal(42, matrikkel.TimeoutSeconds);
        matrikkel.Validate();

        var enrichment = configuration.GetSection("Adresseberikelse").Get<AdresseberikelseOptions>()!;
        Assert.True(enrichment.Enabled);
        Assert.Equal(17, enrichment.BatchSize);
        Assert.Equal(123, enrichment.LookupDelayMs);
        Assert.Equal(23, enrichment.IntervalMinutes);
    }
}
