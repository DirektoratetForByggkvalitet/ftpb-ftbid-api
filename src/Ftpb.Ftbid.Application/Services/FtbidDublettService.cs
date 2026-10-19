using System.Text;
using FtbidEntitet = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Matcher kun kombinasjonen av prosjektnavn, prosjektnummer, tiltakstype og matrikkel.</summary>
public sealed class FtbidDublettService : IFtbidDublettService
{
    /// <inheritdoc />
    public FtbidDublettTreff? Finn(FtbidEntitet prosjekt, IEnumerable<FtbidEntitet> kandidater)
    {
        var treff = kandidater.Where(k =>
                Lik(prosjekt.Prosjektnavn, k.Prosjektnavn) &&
                Lik(prosjekt.Prosjektnummer, k.Prosjektnummer) &&
                Lik(prosjekt.Tiltakstype, k.Tiltakstype) &&
                prosjekt.Eiendom.Any(a => k.Eiendom.Any(b =>
                    a.Eiendomsidentifikasjon.Kommunenummer == b.Eiendomsidentifikasjon.Kommunenummer &&
                    a.Eiendomsidentifikasjon.Gaardsnummer == b.Eiendomsidentifikasjon.Gaardsnummer &&
                    a.Eiendomsidentifikasjon.Bruksnummer == b.Eiendomsidentifikasjon.Bruksnummer)))
            .OrderBy(k => k.Ean, StringComparer.Ordinal).FirstOrDefault();
        return treff is null ? null : new(treff.Ean,
            ["Prosjektnavn", "Prosjektnummer", "Tiltakstype", "Matrikkelopplysninger"]);
    }

    private static string Normaliser(string? verdi) => string.Join(' ',
        (verdi ?? "").Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    private static bool Lik(string? a, string? b) => Normaliser(a) is { Length: > 0 } normalisert && normalisert == Normaliser(b);
}
