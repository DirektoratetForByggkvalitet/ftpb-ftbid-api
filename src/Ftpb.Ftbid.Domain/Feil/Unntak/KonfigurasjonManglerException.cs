using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>En nødvendig konfigurasjonsverdi mangler ved oppstart.</summary>
public sealed class KonfigurasjonManglerException(string verdiNavn)
    : FtbidException($"Konfigurasjonsverdien {verdiNavn} må fylles ut.", Feilkode.V0059, FeilType.Feil)
{
    public string VerdiNavn { get; } = verdiNavn;
}
