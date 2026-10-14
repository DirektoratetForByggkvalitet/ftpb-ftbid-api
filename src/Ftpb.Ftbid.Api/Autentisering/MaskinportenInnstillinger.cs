namespace Ftpb.Ftbid.Api.Autentisering;

/// <summary>Innstillinger for validering av innkommende Maskinporten-token.</summary>
public sealed class MaskinportenInnstillinger
{
    /// <summary>Aktiverer tokenvalidering og scopekrav. Når av, er API-et tilgjengelig uten token.</summary>
    public bool Aktivert { get; set; } = true;
    public string Utsteder { get; set; } = "https://test.maskinporten.no/";
    public string LesScope { get; set; } = "dibk:bygg/ftbid.les";
    public string EndreScope { get; set; } = "dibk:bygg/ftbid.endre";
    public string? Publikum { get; set; }
}
