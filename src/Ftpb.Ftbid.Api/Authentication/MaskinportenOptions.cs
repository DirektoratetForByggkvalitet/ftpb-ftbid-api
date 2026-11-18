using Microsoft.Extensions.Configuration;

namespace Ftpb.Ftbid.Api.Authentication;

/// <summary>Innstillinger for validering av innkommende Maskinporten-token.</summary>
public sealed class MaskinportenOptions
{
    /// <summary>Aktiverer tokenvalidering og scopekrav. Når av, er API-et tilgjengelig uten token.</summary>
    [ConfigurationKeyName("Aktivert")]
    public bool Enabled { get; set; } = true;
    [ConfigurationKeyName("Utsteder")]
    public string Issuer { get; set; } = "https://test.maskinporten.no/";
    [ConfigurationKeyName("LesScope")]
    public string ReadScope { get; set; } = "dibk:bygg/ftbid.les";
    [ConfigurationKeyName("EndreScope")]
    public string WriteScope { get; set; } = "dibk:bygg/ftbid.endre";
    [ConfigurationKeyName("Publikum")]
    public string? Audience { get; set; }
}
