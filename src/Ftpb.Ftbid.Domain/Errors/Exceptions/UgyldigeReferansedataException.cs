using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>Importfilens referansedata har ugyldig struktur.</summary>
public sealed class UgyldigeReferansedataException(Exception? innerException = null)
    : ValidationException($"Kommuner og skjemaer må være gyldige lister.", Feilkode.V0055, innerException)
{
}
