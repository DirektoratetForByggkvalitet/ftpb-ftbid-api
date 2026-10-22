namespace Ftpb.Ftbid.Domain.Prosjekter;

/// <summary>Et prosjekt med en entydig fellesserviceidentifikator.</summary>
public sealed class Ftbid
{
    public string? EksternProsjektreferanse { get; set; }
    public string? KommunaltSaksnummer { get; set; }
    public string? Tiltakshaver { get; set; }
    public string? Organisasjonsnummer { get; set; }
    public string? AnsvarligSokerOrganisasjonsnummer { get; set; }
    public List<string> Tiltakstyper { get; set; } = [];
    // Eldre migrerte poster har null og sammenlignes med beregnet hash.
    public string? TiltakstyperHash { get; set; }
    public string Ean { get; set; } = "";
    public string Prosjektnavn { get; set; } = "";
    public string NormalisertProsjektnavn { get; set; } = "";
    public string? Prosjektnummer { get; set; }
    public string System { get; set; } = "";
    public DateTimeOffset Utstedt { get; set; }
    public List<Eiendom> Eiendom { get; set; } = [];
    public List<Identifikasjonsmetadata> Identifikasjonsmetadata { get; set; } = [];
}
