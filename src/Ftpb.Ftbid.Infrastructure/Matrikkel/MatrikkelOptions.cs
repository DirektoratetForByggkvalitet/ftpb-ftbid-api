using Microsoft.Extensions.Configuration;

namespace Ftpb.Ftbid.Infrastructure.Matrikkel;

/// <summary>Tilkobling til Kartverkets Matrikkel SOAP-API. Hemmeligheter settes i kjøremiljøet.</summary>
public sealed class MatrikkelOptions
{
    [ConfigurationKeyName("BasisUrl")]
    public string BaseUrl { get; set; } = "https://nd.matrikkel.no/matrikkelapi/wsapi/v1";
    [ConfigurationKeyName("Brukernavn")]
    public string Username { get; set; } = "";
    [ConfigurationKeyName("Passord")]
    public string Password { get; set; } = "";
    [ConfigurationKeyName("TidsavbruddSekunder")]
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Kontrollerer tilkoblingen før en aktiv jobb begynner å lese eiendomsrader.</summary>
    public void Validate()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password) || TimeoutSeconds is < 1 or > 600)
            throw new InvalidOperationException("Matrikkel krever HTTPS BasisUrl, Brukernavn, Passord og TidsavbruddSekunder mellom 1 og 600.");
    }
}
