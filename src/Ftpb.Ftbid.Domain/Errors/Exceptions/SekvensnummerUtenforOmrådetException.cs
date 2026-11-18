using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>Sekvensnummeret kan ikke representeres i ftbid-formatet.</summary>
public sealed class SekvensnummerUtenforOmrådetException(long sekvensnummer, Exception? innerException = null)
    : FtbidException($"Sekvensnummeret {sekvensnummer} må være mellom 1 og 9999999.", Feilkode.V0058, FeilType.Feil, innerException)
{
    public long Sekvensnummer { get; } = sekvensnummer;
}
