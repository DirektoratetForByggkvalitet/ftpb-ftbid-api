using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>Gyldighetsperioden er i feil rekkefølge.</summary>
public sealed class GyldigFraEtterGyldigTilException(Exception? innerException = null)
    : ValidationException($"Gyldig fra kan ikke være etter gyldig til.", Feilkode.V0026, innerException)
{
}
