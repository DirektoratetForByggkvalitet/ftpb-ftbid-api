namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Eiendommen finnes ikke på denne ftbid-en.</summary>
public sealed class EiendomIkkeFunnetException(long eiendomId)
    : ValideringsException($"Eiendommen {eiendomId} finnes ikke på denne ftbid-en.", Feilkode.V0071);
