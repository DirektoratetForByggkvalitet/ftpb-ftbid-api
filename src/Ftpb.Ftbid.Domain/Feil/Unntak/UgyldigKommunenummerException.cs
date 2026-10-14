using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Kommunenummeret har ugyldig format eller er ikke gyldig i kommuneregisteret.</summary>
public sealed class UgyldigKommunenummerException(string? kommunenummer, Exception? indreUnntak = null)
    : ValideringsException($"Ugyldig kommunenummer ({kommunenummer}).", Feilkode.V0034, indreUnntak)
{
    public string? Kommunenummer { get; } = kommunenummer;
}
