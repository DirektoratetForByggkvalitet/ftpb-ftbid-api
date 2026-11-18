using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>GS1-prefikset i servicens konfigurasjon er ugyldig.</summary>
public sealed class UgyldigGs1PrefiksException(Exception? innerException = null)
    : FtbidException($"GS1-prefikset må bestå av sju sifre.", Feilkode.V0057, FeilType.Feil, innerException)
{
}
