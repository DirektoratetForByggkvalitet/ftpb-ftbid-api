namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Filtre som kombineres ved søk. Matrikkelfiltre må gjelde samme matrikkelenhet.</summary>
public sealed record FtbidSok(string? Kommunenummer = null, int? Gaardsnummer = null,
    int? Bruksnummer = null, string? Prosjektnavn = null, string? Adresse = null,
    string? Ftbid = null, string? Tiltaksnavn = null,
    string? Tiltakstype = null, string? EksternProsjektreferanse = null, string? KommunaltSaksnummer = null,
    string? AnsvarligSokerOrganisasjonsnummer = null, string? Bygningsnummer = null,
    string? Bruksenhetsnummer = null, int? Festenummer = null, int? Seksjonsnummer = null);
