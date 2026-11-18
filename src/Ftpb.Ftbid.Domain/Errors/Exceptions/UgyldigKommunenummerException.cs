using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>Kommunenummeret har ugyldig format eller er ikke gyldig i kommuneregisteret.</summary>
public sealed class UgyldigKommunenummerException(string? kommunenummer, Exception? innerException = null)
    : ValidationException($"Ugyldig kommunenummer ({kommunenummer}).", Feilkode.V0034, innerException)
{
    public string? Kommunenummer { get; } = kommunenummer;
}
