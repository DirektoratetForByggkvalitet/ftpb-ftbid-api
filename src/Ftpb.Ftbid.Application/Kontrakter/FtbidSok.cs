namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Filtre som kombineres ved søk. Matrikkelfiltre må gjelde samme matrikkelenhet.</summary>
public sealed record FtbidSok(string? Kommunenummer = null, int? Gaardsnummer = null,
    int? Bruksnummer = null, string? Prosjektnavn = null, string? Adresse = null,
    string? Ftbid = null, string? Tiltaksnavn = null,
    string? Tiltakstype = null, string? EksternProsjektreferanse = null, string? KommunaltSaksnummer = null,
    string? AnsvarligSokerOrganisasjonsnummer = null, string? Bygningsnummer = null,
    string? Bolignummer = null, int? Festenummer = null, int? Seksjonsnummer = null)
{
    /// <summary>Tom eller blank tekst regnes ikke som et søkefilter.</summary>
    public bool HarFiltre() => Gaardsnummer.HasValue || Bruksnummer.HasValue || Festenummer.HasValue ||
        Seksjonsnummer.HasValue || new[] { Kommunenummer, Prosjektnavn, Adresse, Ftbid, Tiltaksnavn,
            Tiltakstype, EksternProsjektreferanse, KommunaltSaksnummer, AnsvarligSokerOrganisasjonsnummer,
            Bygningsnummer, Bolignummer }.Any(verdi => !string.IsNullOrWhiteSpace(verdi));
}
