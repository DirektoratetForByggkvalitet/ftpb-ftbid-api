using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>Identifikatoren har ugyldig format.</summary>
public sealed class UgyldigFtbidException(Exception? innerException = null)
    : ValidationException($"Ftbid må bestå av 18 sifre eller de siste åtte sifrene.", Feilkode.V0052, innerException)
{
}
