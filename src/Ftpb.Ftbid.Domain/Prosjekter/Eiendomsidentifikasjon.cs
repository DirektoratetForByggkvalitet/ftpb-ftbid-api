namespace Ftpb.Ftbid.Domain.Prosjekter;

/// <summary>Matrikkelidentifikasjon for én eiendom, uten eksterne oppslag.</summary>
public sealed record Eiendomsidentifikasjon(string Kommunenummer, int Gaardsnummer, int Bruksnummer,
    int Festenummer = 0, int Seksjonsnummer = 0);
