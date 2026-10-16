namespace Ftpb.Ftbid.Domain.Prosjekter;

/// <summary>Et prosjekt med en entydig fellesserviceidentifikator.</summary>
public sealed class Ftbid
{
    public string Kommunenummer { get; set; } = "";
    public int Gaardsnummer { get; set; }
    public int Bruksnummer { get; set; }
    public int Festenummer { get; set; }
    public int Seksjonsnummer { get; set; }
    public string? EksternProsjektreferanse { get; set; }
    public string? KommunaltSaksnummer { get; set; }
    public string? Tiltakshaver { get; set; }
    public string? Organisasjonsnummer { get; set; }
    public string? AnsvarligSokerOrganisasjonsnummer { get; set; }
    public string? Bruksenhetsnummer { get; set; }
    public string? Bygningsnummer { get; set; }
    public string? Tiltakstype { get; set; }
    public string? Adresse { get; set; }
    public string Ean { get; set; } = "";
    public string Prosjektnavn { get; set; } = "";
    public string NormalisertProsjektnavn { get; set; } = "";
    public string? Prosjektnummer { get; set; }
    public string System { get; set; } = "";
    public DateTimeOffset Utstedt { get; set; }
    public List<Adresse> Adresser { get; set; } = [];
    public List<Matrikkelopplysning> Matrikkelopplysninger { get; set; } = [];
    public List<Identifikasjonsmetadata> Identifikasjonsmetadata { get; set; } = [];
}
