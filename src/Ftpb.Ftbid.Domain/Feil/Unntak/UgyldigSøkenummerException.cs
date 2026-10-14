using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Et matrikkelnummer brukt i søk er negativt.</summary>
public sealed class UgyldigSøkenummerException(Exception? indreUnntak = null)
    : ValideringsException($"Søkenummer kan ikke være negative.", Feilkode.V0051, indreUnntak)
{
}
