using Ftpb.Ftbid.Domain.Errors;
using Ftpb.Ftbid.Domain.Errors.Exceptions;
using Xunit;

namespace Ftpb.Ftbid.Tests.Unit;

/// <summary>Verifiserer regelkoder, kontekst og grunnklassene for forventede unntak.</summary>
public sealed class FtbidExceptionTests
{
    /// <summary>Regler med samme betydning beholder NIREGs faste koder.</summary>
    [Fact]
    public void DelteReglerBeholderNiregKoder()
    {
        var kommune = new UgyldigKommunenummerException("0301");
        Assert.Equal(Feilkode.V0034, kommune.Kode);
        Assert.Equal("0301", kommune.Kommunenummer);
        Assert.Equal(Feilkode.V0042, new RequiredValueMissingException("Prosjektnavn").Kode);
        Assert.Equal(Feilkode.V0038, new DeserializationFailedException("referansedata").Kode);
        Assert.Equal(Feilkode.V0026, new GyldigFraEtterGyldigTilException().Kode);
        Assert.Equal(Feilkode.V003, new ImportFileMissingOrEmptyException().Kode);
        Assert.IsAssignableFrom<ValidationException>(kommune);
        Assert.True(typeof(FtbidException).IsAbstract);
    }

    /// <summary>Unntakene bevarer årsak, norsk beskrivelse og feiltype.</summary>
    [Fact]
    public void UnntakBevarerAarsakOgKontekst()
    {
        var aarsak = new FormatException("Testårsak");
        var error = new FtbidIkkeFunnetException("709999900000000017", aarsak);
        Assert.Same(aarsak, error.InnerException);
        Assert.Equal(FeilType.Feil, error.FeilType);
        Assert.Equal(Feilkode.V0054, error.Kode);
        Assert.Contains("V0054", error.ToString());
        Assert.Equal("709999900000000017", error.Ftbid);
        var sekvens = new SekvensnummerUtenforOmrådetException(10_000_000);
        Assert.IsAssignableFrom<FtbidException>(sekvens);
        Assert.IsNotAssignableFrom<ValidationException>(sekvens);
    }
}
