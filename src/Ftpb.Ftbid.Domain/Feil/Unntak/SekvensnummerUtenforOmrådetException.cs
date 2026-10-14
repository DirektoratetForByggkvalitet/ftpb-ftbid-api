using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Sekvensnummeret kan ikke representeres i ftbid-formatet.</summary>
public sealed class SekvensnummerUtenforOmrådetException(long sekvensnummer, Exception? indreUnntak = null)
    : FtbidException($"Sekvensnummeret {sekvensnummer} må være mellom 1 og 9999999.", Feilkode.V0058, FeilType.Feil, indreUnntak)
{
    public long Sekvensnummer { get; } = sekvensnummer;
}
