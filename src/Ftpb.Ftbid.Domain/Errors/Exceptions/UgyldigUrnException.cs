namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>ReferanseId må ha gyldig URN-syntaks.</summary>
public sealed class UgyldigUrnException() : ValidationException(
    "ReferanseId må være en gyldig URN på formen urn:navnerom:referanse, for eksempel urn:dibk:bygg:innsending-id:65465411-34184-13213.",
    Feilkode.V0073);
