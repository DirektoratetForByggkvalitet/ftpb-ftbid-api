using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>Et matrikkelnummer brukt i søk er negativt.</summary>
public sealed class UgyldigSøkenummerException(Exception? innerException = null)
    : ValidationException($"Søkenummer kan ikke være negative.", Feilkode.V0051, innerException)
{
}
