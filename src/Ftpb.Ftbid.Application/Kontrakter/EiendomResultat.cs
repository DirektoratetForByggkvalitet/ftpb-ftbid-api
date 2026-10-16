using Ftpb.Ftbid.Domain.Prosjekter;

namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Eiendom med stabil ID til bruk ved endring eller sletting.</summary>
public sealed record EiendomResultat(long Id, Eiendomsidentifikasjon Eiendomsidentifikasjon,
    List<Adressedata> Adresse, string? Bygningsnummer, string? Bolignummer, string? Kommunenavn);
