namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Matrikkeldata i forespørsler og svar.</summary>
public sealed record Matrikkeldata(string Kommunenummer, int Gaardsnummer, int Bruksnummer,
    int Festenummer = 0, int Seksjonsnummer = 0);
