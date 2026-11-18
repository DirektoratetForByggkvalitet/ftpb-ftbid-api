using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>Referansen finnes ikke i metadata (historisk unntaksnavn).</summary>
public sealed class ArkivreferanseIkkeFunnetException(string arkivreferanse, Exception? innerException = null)
    : ValidationException($"Fant ingen metadata med ReferanseId {arkivreferanse}.", Feilkode.V0053, innerException)
{
    public string Arkivreferanse { get; } = arkivreferanse;
}
