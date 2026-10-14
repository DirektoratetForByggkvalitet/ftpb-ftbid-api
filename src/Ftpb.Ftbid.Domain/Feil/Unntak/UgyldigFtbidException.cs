using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Identifikatoren har ugyldig format.</summary>
public sealed class UgyldigFtbidException(Exception? indreUnntak = null)
    : ValideringsException($"Ftbid må bestå av 18 sifre eller de siste åtte sifrene.", Feilkode.V0052, indreUnntak)
{
}
