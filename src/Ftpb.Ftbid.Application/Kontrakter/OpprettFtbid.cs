using Ftpb.Ftbid.Domain.Prosjekter;

namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Opprettingsdata. JSON-feltnavnene er på norsk.</summary>
public sealed class OpprettFtbid
{
    public string? Kommunenummer { get; set; }
    public int? Gaardsnummer { get; set; }
    public int? Bruksnummer { get; set; }
    public int? Festenummer { get; set; }
    public int? Seksjonsnummer { get; set; }
    public string? EksternProsjektreferanse { get; set; }
    public string? KommunaltSaksnummer { get; set; }
    public string? Tiltakshaver { get; set; }
    public string? Organisasjonsnummer { get; set; }
    public string? Bygningsnummer { get; set; }
    public string? Tiltakstype { get; set; }
    public string? Adresse { get; set; }
    public string? Prosjektnavn { get; set; }
    public string? Prosjektnr { get; set; }
    public string? System { get; set; }
    public List<string>? Adresser { get; set; }
    public List<Matrikkeldata>? Matrikkelopplysninger { get; set; }
}
