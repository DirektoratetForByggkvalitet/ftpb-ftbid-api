using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Eiendommen gir samme prosjektidentitet som et annet prosjekt.</summary>
public sealed class ProsjektidentitetAlleredeIBrukException(string ftbid, Exception? indreUnntak = null)
    : ValideringsException($"Eiendommen kan ikke legges til eller endres fordi prosjektidentiteten allerede er i bruk. Ftbid: {ftbid}.", Feilkode.V0062, indreUnntak)
{
    public string Ftbid { get; } = ftbid;
}
