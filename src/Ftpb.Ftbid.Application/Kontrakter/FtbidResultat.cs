namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Prosjektdata med eiendommer og tilknyttede skjemaer.</summary>
public sealed class FtbidResultat
{
    public string Ftbid { get; set; } = "";
    public string? EksternProsjektreferanse { get; set; }
    public string? KommunaltSaksnummer { get; set; }
    public string? Tiltakshaver { get; set; }
    public string? Organisasjonsnummer { get; set; }
    public string? AnsvarligSokerOrganisasjonsnummer { get; set; }
    public List<EiendomResultat> Eiendom { get; set; } = [];
    public string? Tiltakstype { get; set; }
    public string? Prosjektnummer { get; set; }
    public string Prosjektnavn { get; set; } = "";
    public List<TilknyttetSkjema> TilknyttedeSkjemaer { get; set; } = [];
}
