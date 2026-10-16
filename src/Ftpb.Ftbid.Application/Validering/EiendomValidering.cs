using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Lagring;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using Ftpb.Ftbid.Domain.Prosjekter;
using static Ftpb.Ftbid.Application.Validering.FtbidValidering;

namespace Ftpb.Ftbid.Application.Validering;

/// <summary>Validerer eiendomsdata lokalt og bygger lagringsmodellen.</summary>
internal static class EiendomValidering
{
    internal static async Task<Eiendom> TilEntitetAsync(Eiendomsdata? data, IKommuneregister kommuner, CancellationToken avbrudd)
    {
        if (data?.Eiendomsidentifikasjon is null) throw new PåkrevdVerdiManglerException("Eiendomsidentifikasjon");
        var identifikasjon = await MatrikkelValidering.ValiderNyAsync(data.Eiendomsidentifikasjon, kommuner, avbrudd);
        if (data.Adresse is null) throw new PåkrevdVerdiManglerException("Adresse");
        if (data.Adresse.Count > 100) throw new ForMangeVerdierException("Adresse", 100);
        var adresser = new List<Eiendomsadresse>();
        foreach (var adresse in data.Adresse)
        {
            if (adresse is null) throw new PåkrevdVerdiManglerException("Adresse");
            var a = new Eiendomsadresse {
                Adresselinje1 = Tekst(adresse.Adresselinje1, "Adresselinje1", 1000),
                Adresselinje2 = Tekst(adresse.Adresselinje2, "Adresselinje2", 1000),
                Adresselinje3 = Tekst(adresse.Adresselinje3, "Adresselinje3", 1000),
                Postnr = Tekst(adresse.Postnr, "Postnr", 20), Poststed = Tekst(adresse.Poststed, "Poststed", 200),
                Landkode = Tekst(adresse.Landkode, "Landkode", 2)?.ToUpperInvariant(),
                Gatenavn = Tekst(adresse.Gatenavn, "Gatenavn", 500), Husnr = Tekst(adresse.Husnr, "Husnr", 50),
                Bokstav = Tekst(adresse.Bokstav, "Bokstav", 20), Rekkefolge = adresser.Count };
            if (new[] { a.Adresselinje1, a.Adresselinje2, a.Adresselinje3, a.Postnr, a.Poststed, a.Landkode,
                a.Gatenavn, a.Husnr, a.Bokstav }.All(v => v is null)) throw new PåkrevdVerdiManglerException("Adresseinnhold");
            adresser.Add(a);
        }
        return new Eiendom { Eiendomsidentifikasjon = identifikasjon, Adresse = adresser,
            Bygningsnummer = Tekst(data.Bygningsnummer, "Bygningsnummer", 50),
            Bolignummer = Tekst(data.Bolignummer, "Bolignummer", 50),
            Kommunenavn = Tekst(data.Kommunenavn, "Kommunenavn", 200) };
    }

    private static string? Tekst(string? verdi, string navn, int maks) =>
        string.IsNullOrWhiteSpace(verdi) ? null : KrevTekst(verdi, navn, maks);
}
