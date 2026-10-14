namespace Ftpb.Ftbid.Domain.Prosjekter;

/// <summary>En matrikkelenhet knyttet til prosjektet.</summary>
public sealed class Matrikkelopplysning
{
    public long Id { get; set; }
    public string Ftbid { get; set; } = "";
    public int Rekkefolge { get; set; }
    public string Kommunenummer { get; set; } = "";
    public int Gaardsnummer { get; set; }
    public int Bruksnummer { get; set; }
    public int Festenummer { get; set; }
    public int Seksjonsnummer { get; set; }
}
