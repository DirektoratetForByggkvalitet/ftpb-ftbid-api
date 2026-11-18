using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>Importerte skjemametadata mangler verdier eller bryter begrensningene.</summary>
public sealed class UgyldigeSkjemametadataException(Exception? innerException = null)
    : ValidationException($"Importen inneholder ugyldige skjemametadata.", Feilkode.V0056, innerException)
{
}
