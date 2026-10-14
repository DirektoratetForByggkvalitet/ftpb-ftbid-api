using Ftpb.Ftbid.Api.Autentisering;
using Ftpb.Ftbid.Domain.Identifikatorer;
using System.Security.Claims;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using Xunit;

namespace Ftpb.Ftbid.Tests.Enhet;

/// <summary>Kontrollerer EAN-format og presis tolkning av Maskinporten-claims.</summary>
public sealed class EanOgMaskinportenTester
{
    /// <summary>Bevarer prefiks og kontrollsiffer fra adapterens algoritme.</summary>
    [Theory]
    [InlineData(1, "709999900000000017")]
    [InlineData(2, "709999900000000024")]
    public void EanHarForventetKontrollsiffer(long sekvens, string forventet) =>
        Assert.Equal(forventet, EanGenerator.Lag("7099999", sekvens));

    /// <summary>Avviser sekvensnumre som ikke får plass i identifikatoren.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(10000000)]
    public void SekvensUtenforOmraadetAvvises(long sekvens) =>
        Assert.Throws<SekvensnummerUtenforOmrådetException>(() => EanGenerator.Lag("7099999", sekvens));

    /// <summary>Scopeverdier matches eksakt også når flere verdier ligger i samme claim.</summary>
    [Fact]
    public void ScopeverdierMatchesEksakt()
    {
        var bruker = new ClaimsPrincipal(new ClaimsIdentity([new Claim("scope", "annet dibk:bygg/ftbid.les"), new Claim("scope", "dibk:bygg/ftbid.endre")]));
        Assert.True(MaskinportenAutentisering.HarScope(bruker, "dibk:bygg/ftbid.les"));
        Assert.True(MaskinportenAutentisering.HarScope(bruker, "dibk:bygg/ftbid.endre"));
        Assert.False(MaskinportenAutentisering.HarScope(bruker, "dibk:bygg/ftbid"));
    }
}
