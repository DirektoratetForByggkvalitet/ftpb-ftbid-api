using Ftpb.Ftbid.Domain.Prosjekter;
using System.Text.Json.Serialization;

namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Prosjektdata med både tidligere enkeltfelter og nye flerverdifelter.</summary>
public sealed class FtbidResultat
{
    public string Ftbid { get; set; } = "";
    public string Kommunenummer { get; set; } = "";
    public int Gaardsnummer { get; set; }
    public int Bruksnummer { get; set; }
    public int Festenummer { get; set; }
    public int Seksjonsnummer { get; set; }
    public string? EksternProsjektreferanse { get; set; }
    public string? KommunaltSaksnummer { get; set; }
    public string? Tiltakshaver { get; set; }
    public string? Organisasjonsnummer { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter<FtIdType>))]
    public FtIdType FtIdType { get; set; } = FtIdType.Ftbid;
    public string? AnsvarligSokerOrganisasjonsnummer { get; set; }
    public string? Bruksenhetsnummer { get; set; }
    public string? Bygningsnummer { get; set; }
    public string? Tiltakstype { get; set; }
    public string? Adresse { get; set; }
    public string? Prosjektnummer { get; set; }
    public string Prosjektnavn { get; set; } = "";
    public List<string> Adresser { get; set; } = [];
    public List<Matrikkeldata> Matrikkelopplysninger { get; set; } = [];
    public List<TilknyttetSkjema> TilknyttedeSkjemaer { get; set; } = [];
}
