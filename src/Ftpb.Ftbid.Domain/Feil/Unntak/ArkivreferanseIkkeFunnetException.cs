using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Referansen finnes ikke i metadata (historisk unntaksnavn).</summary>
public sealed class ArkivreferanseIkkeFunnetException(string arkivreferanse, Exception? indreUnntak = null)
    : ValideringsException($"Fant ingen metadata med ReferanseId {arkivreferanse}.", Feilkode.V0053, indreUnntak)
{
    public string Arkivreferanse { get; } = arkivreferanse;
}
