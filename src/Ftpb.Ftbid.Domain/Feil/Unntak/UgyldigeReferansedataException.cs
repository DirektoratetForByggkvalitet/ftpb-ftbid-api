using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Importfilens referansedata har ugyldig struktur.</summary>
public sealed class UgyldigeReferansedataException(Exception? indreUnntak = null)
    : ValideringsException($"Kommuner og skjemaer må være gyldige lister.", Feilkode.V0055, indreUnntak)
{
}
