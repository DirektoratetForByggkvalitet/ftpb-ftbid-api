namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Opprettingen kolliderer med et eksisterende prosjekt og skal avvises uten endringer.</summary>
public sealed class FtbidDublettException(string ftbid, IReadOnlyList<string> samsvarendeFelter)
    : FtbidException($"Prosjektnavn, prosjektnummer, tiltakstyper og matrikkelopplysninger finnes allerede på ftbid {ftbid}.",
        Feilkode.V0067, FeilType.Feil)
{
    public string Ftbid { get; } = ftbid;
    public IReadOnlyList<string> SamsvarendeFelter { get; } = samsvarendeFelter;
}
