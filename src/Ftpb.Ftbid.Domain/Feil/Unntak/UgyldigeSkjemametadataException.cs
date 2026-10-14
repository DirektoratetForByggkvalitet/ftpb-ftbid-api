using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Importerte skjemametadata mangler verdier eller bryter begrensningene.</summary>
public sealed class UgyldigeSkjemametadataException(Exception? indreUnntak = null)
    : ValideringsException($"Importen inneholder ugyldige skjemametadata.", Feilkode.V0056, indreUnntak)
{
}
