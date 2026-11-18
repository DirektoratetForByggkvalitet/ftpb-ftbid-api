namespace Ftpb.Ftbid.Application.Contracts;

/// <summary>Prosjektdata med eiendommer og tilknytninger til skjemaer, rapporter og andre ressurser.</summary>
public sealed class FtbidResult
{
    public string Ftbid { get; set; } = "";
    public DateTimeOffset Utstedt { get; set; }
    public string? EksternProsjektreferanse { get; set; }
    public string? KommunaltSaksnummer { get; set; }
    public string? Tiltakshaver { get; set; }
    public string? Organisasjonsnummer { get; set; }
    public string? AnsvarligSokerOrganisasjonsnummer { get; set; }
    public List<EiendomResult> Eiendom { get; set; } = [];
    public List<string> Tiltakstyper { get; set; } = [];
    public string? Prosjektnummer { get; set; }
    public string Prosjektnavn { get; set; } = "";
    public List<Tilknytning> Tilknytninger { get; set; } = [];
}
