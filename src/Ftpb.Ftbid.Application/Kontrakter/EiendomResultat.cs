using Ftpb.Ftbid.Domain.Prosjekter;

namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Eiendom med stabil ID til bruk ved endring eller sletting.</summary>
public sealed record EiendomResultat(long Id, Eiendomsidentifikasjon Eiendomsidentifikasjon,
    List<Adressedata> Adresse, string? Bygningsnummer, string? Bolignummer, string? Kommunenavn)
{
    public static EiendomResultat FraEntitet(Eiendom eiendom) => new(eiendom.Id, eiendom.Eiendomsidentifikasjon,
        eiendom.Adresse.OrderBy(a => a.Rekkefolge).Select(a => new Adressedata(
            a.Adresselinje1, a.Adresselinje2, a.Adresselinje3, a.Postnr, a.Poststed, a.Landkode,
            a.Gatenavn, a.Husnr, a.Bokstav)).ToList(), eiendom.Bygningsnummer, eiendom.Bolignummer, eiendom.Kommunenavn);
}
