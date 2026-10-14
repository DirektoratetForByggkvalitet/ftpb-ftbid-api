using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Maskinportens utsteder eller nødvendige scopes er feil konfigurert.</summary>
public sealed class UgyldigMaskinportenkonfigurasjonException()
    : FtbidException("Maskinporten krever en HTTPS-utsteder med avsluttende skråstrek og les-/endrescope.",
        Feilkode.V0060, FeilType.Feil);
