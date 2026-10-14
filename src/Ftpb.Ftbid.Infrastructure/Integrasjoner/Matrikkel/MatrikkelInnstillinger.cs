namespace Ftpb.Ftbid.Infrastructure.Integrasjoner.Matrikkel;

/// <summary>SOAP-endepunkt og Basic-autentisering, som i Niregs StatkartMatrikkel-oppsett.</summary>
public sealed class MatrikkelInnstillinger
{
    public string Basisadresse { get; set; } = "https://nd.matrikkel.no/matrikkelapi/wsapi/v1/";
    public string Brukernavn { get; set; } = "";
    public string Passord { get; set; } = "";
    public int TidsgrenseSekunder { get; set; } = 15;
}
