using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Arkivreferansen finnes ikke i skjemametadata.</summary>
public sealed class ArkivreferanseIkkeFunnetException(string arkivreferanse, Exception? indreUnntak = null)
    : ValideringsException($"Fant ingen datapunkter med arkivreferansen {arkivreferanse}.", Feilkode.V0053, indreUnntak)
{
    public string Arkivreferanse { get; } = arkivreferanse;
}
