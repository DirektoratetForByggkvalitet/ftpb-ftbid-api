using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Gyldighetsperioden er i feil rekkefølge.</summary>
public sealed class GyldigFraEtterGyldigTilException(Exception? indreUnntak = null)
    : ValideringsException($"Gyldig fra kan ikke være etter gyldig til.", Feilkode.V0026, indreUnntak)
{
}
