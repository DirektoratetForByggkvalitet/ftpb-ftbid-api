namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>Eiendommen finnes ikke på denne ftbid-en.</summary>
public sealed class EiendomIkkeFunnetException(long eiendomId)
    : ValidationException($"Eiendommen {eiendomId} finnes ikke på denne ftbid-en.", Feilkode.V0071);
