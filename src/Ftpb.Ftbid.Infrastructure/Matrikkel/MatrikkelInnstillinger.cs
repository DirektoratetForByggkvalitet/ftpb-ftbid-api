namespace Ftpb.Ftbid.Infrastructure.Matrikkel;

/// <summary>Tilkobling til Kartverkets Matrikkel SOAP-API. Hemmeligheter settes i kjøremiljøet.</summary>
public sealed class MatrikkelInnstillinger
{
    public string BasisUrl { get; set; } = "https://nd.matrikkel.no/matrikkelapi/wsapi/v1";
    public string Brukernavn { get; set; } = "";
    public string Passord { get; set; } = "";
    public int TidsavbruddSekunder { get; set; } = 60;

    /// <summary>Kontrollerer tilkoblingen før en aktiv jobb begynner å lese eiendomsrader.</summary>
    public void Valider()
    {
        if (!Uri.TryCreate(BasisUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(Brukernavn) || string.IsNullOrWhiteSpace(Passord) || TidsavbruddSekunder is < 1 or > 600)
            throw new InvalidOperationException("Matrikkel krever HTTPS BasisUrl, Brukernavn, Passord og TidsavbruddSekunder mellom 1 og 600.");
    }
}
