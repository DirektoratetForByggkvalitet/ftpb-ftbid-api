using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Prosjektet må beholde minst én matrikkelenhet.</summary>
public sealed class SisteMatrikkelKanIkkeFjernesException(string ftbid, Exception? indreUnntak = null)
    : ValideringsException($"Den siste matrikkelenheten kan ikke fjernes fra prosjektet. Ftbid: {ftbid}.", Feilkode.V0061, indreUnntak)
{
    public string Ftbid { get; } = ftbid;
}
