using Ftpb.Ftbid.Domain.Feil;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using Xunit;

namespace Ftpb.Ftbid.Tests.Enhet;

/// <summary>Verifiserer regelkoder, kontekst og grunnklassene for forventede unntak.</summary>
public sealed class FtbidUnntakTester
{
    /// <summary>Regler med samme betydning beholder NIREGs faste koder.</summary>
    [Fact]
    public void DelteReglerBeholderNiregKoder()
    {
        var kommune = new UgyldigKommunenummerException("0301");
        Assert.Equal(Feilkode.V0034, kommune.Kode);
        Assert.Equal("0301", kommune.Kommunenummer);
        Assert.Equal(Feilkode.V0042, new PåkrevdVerdiManglerException("Prosjektnavn").Kode);
        Assert.Equal(Feilkode.V0038, new DeserialiseringFeiletException("referansedata").Kode);
        Assert.Equal(Feilkode.V0026, new GyldigFraEtterGyldigTilException().Kode);
        Assert.Equal(Feilkode.V003, new ImportfilManglerEllerTomException().Kode);
        Assert.IsAssignableFrom<ValideringsException>(kommune);
        Assert.True(typeof(FtbidException).IsAbstract);
    }

    /// <summary>Unntakene bevarer årsak, norsk beskrivelse og feiltype.</summary>
    [Fact]
    public void UnntakBevarerAarsakOgKontekst()
    {
        var aarsak = new FormatException("Testårsak");
        var feil = new FtbidIkkeFunnetException("709999900000000017", aarsak);
        Assert.Same(aarsak, feil.InnerException);
        Assert.Equal(FeilType.Feil, feil.FeilType);
        Assert.Equal(Feilkode.V0054, feil.Kode);
        Assert.Contains("V0054", feil.ToString());
        Assert.Equal("709999900000000017", feil.Ftbid);
        var sekvens = new SekvensnummerUtenforOmrådetException(10_000_000);
        Assert.IsAssignableFrom<FtbidException>(sekvens);
        Assert.IsNotAssignableFrom<ValideringsException>(sekvens);
    }
}
