namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Opprettingen kolliderer med et eksisterende prosjekt og skal avvises uten endringer.</summary>
public sealed class FtbidDublettException(string ftbid, bool sannsynlig, IReadOnlyList<string> samsvarendeFelter)
    : FtbidException(sannsynlig ? "Prosjektet er en sannsynlig dublett av en eksisterende ftbid." : "Prosjektet er en dublett av en eksisterende ftbid.",
        sannsynlig ? Feilkode.V0068 : Feilkode.V0067, FeilType.Feil)
{
    public string Ftbid { get; } = ftbid;
    public bool Sannsynlig { get; } = sannsynlig;
    public IReadOnlyList<string> SamsvarendeFelter { get; } = samsvarendeFelter;
}
