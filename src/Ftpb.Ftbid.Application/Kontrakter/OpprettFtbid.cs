using System.Text.Json.Serialization;
namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Opprettingsdata. JSON-feltnavnene er på norsk.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class OpprettFtbid
{
    public string? EksternProsjektreferanse { get; set; }
    public string? KommunaltSaksnummer { get; set; }
    public string? Tiltakshaver { get; set; }
    public string? Organisasjonsnummer { get; set; }
    public string? AnsvarligSokerOrganisasjonsnummer { get; set; }
    public List<Eiendomsdata> Eiendom { get; set; } = [];
    public List<string> Tiltakstyper { get; set; } = [];
    public string? Prosjektnavn { get; set; }
    public string? Prosjektnr { get; set; }
    public string? System { get; set; }
}
