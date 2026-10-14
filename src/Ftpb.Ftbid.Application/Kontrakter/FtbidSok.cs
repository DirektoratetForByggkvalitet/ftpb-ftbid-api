using Ftpb.Ftbid.Domain.Prosjekter;

namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Filtre som kombineres ved søk. Matrikkelfiltre må gjelde samme matrikkelenhet.</summary>
public sealed record FtbidSok(string? Kommunenummer = null, int? Gaardsnummer = null,
    int? Bruksnummer = null, string? Prosjektnavn = null, string? Adresse = null);
