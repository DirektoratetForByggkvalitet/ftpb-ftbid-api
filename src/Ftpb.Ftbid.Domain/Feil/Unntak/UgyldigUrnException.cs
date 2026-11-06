namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>ReferanseId må ha gyldig URN-syntaks.</summary>
public sealed class UgyldigUrnException() : ValideringsException(
    "ReferanseId må være en gyldig URN på formen urn:navnerom:referanse, for eksempel urn:dibk:bygg:innsending-id:65465411-34184-13213.",
    Feilkode.V0073);
