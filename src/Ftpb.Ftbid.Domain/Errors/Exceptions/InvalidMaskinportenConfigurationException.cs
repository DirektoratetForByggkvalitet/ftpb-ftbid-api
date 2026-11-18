using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>Maskinportens utsteder eller nødvendige scopes er feil konfigurert.</summary>
public sealed class InvalidMaskinportenConfigurationException()
    : FtbidException("Maskinporten krever en HTTPS-utsteder med avsluttende skråstrek og les-/endrescope.",
        Feilkode.V0060, FeilType.Feil);
