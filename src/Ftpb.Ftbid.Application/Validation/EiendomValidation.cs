using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Application.Repositories;
using Ftpb.Ftbid.Domain.Errors.Exceptions;
using Ftpb.Ftbid.Domain.Prosjekter;
using static Ftpb.Ftbid.Application.Validation.FtbidValidation;

namespace Ftpb.Ftbid.Application.Validation;

/// <summary>Validerer eiendomsdata lokalt og bygger lagringsmodellen.</summary>
internal static class EiendomValidation
{
    internal static async Task<Eiendom> ToEntityAsync(Eiendomsdata? data, IKommuneregister kommuner, CancellationToken cancellationToken)
    {
        if (data?.Eiendomsidentifikasjon is null) throw new RequiredValueMissingException("Eiendomsidentifikasjon");
        var identifikasjon = await MatrikkelValidation.ValidateNewAsync(data.Eiendomsidentifikasjon, kommuner, cancellationToken);
        if (data.Adresse is null) throw new RequiredValueMissingException("Adresse");
        if (data.Adresse.Count > 100) throw new TooManyValuesException("Adresse", 100);
        var adresser = new List<Eiendomsadresse>();
        foreach (var adresse in data.Adresse)
        {
            if (adresse is null) throw new RequiredValueMissingException("Adresse");
            var a = new Eiendomsadresse {
                Adresselinje1 = Text(adresse.Adresselinje1, "Adresselinje1", 1000),
                Adresselinje2 = Text(adresse.Adresselinje2, "Adresselinje2", 1000),
                Adresselinje3 = Text(adresse.Adresselinje3, "Adresselinje3", 1000),
                Postnr = Text(adresse.Postnr, "Postnr", 20), Poststed = Text(adresse.Poststed, "Poststed", 200),
                Landkode = Text(adresse.Landkode, "Landkode", 2)?.ToUpperInvariant(),
                Gatenavn = Text(adresse.Gatenavn, "Gatenavn", 500), Husnr = Text(adresse.Husnr, "Husnr", 50),
                Bokstav = Text(adresse.Bokstav, "Bokstav", 20), Rekkefolge = adresser.Count };
            if (new[] { a.Adresselinje1, a.Adresselinje2, a.Adresselinje3, a.Postnr, a.Poststed, a.Landkode,
                a.Gatenavn, a.Husnr, a.Bokstav }.All(v => v is null)) throw new RequiredValueMissingException("Adresseinnhold");
            adresser.Add(a);
        }
        return new Eiendom { Eiendomsidentifikasjon = identifikasjon, Adresse = adresser,
            Bygningsnummer = Text(data.Bygningsnummer, "Bygningsnummer", 50),
            Bolignummer = Text(data.Bolignummer, "Bolignummer", 50),
            Kommunenavn = Text(data.Kommunenavn, "Kommunenavn", 200) };
    }

    private static string? Text(string? value, string name, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : RequireText(value, name, maxLength);
}
