using System.Text.Json.Serialization;

namespace Ftpb.Ftbid.Domain.Prosjekter;

/// <summary>Matrikkelidentifikasjon for én eiendom, uten eksterne oppslag.</summary>
public sealed record Eiendomsidentifikasjon([property: JsonRequired] string Kommunenummer,
    [property: JsonRequired] int Gaardsnummer, [property: JsonRequired] int Bruksnummer,
    int Festenummer = 0, int Seksjonsnummer = 0);
