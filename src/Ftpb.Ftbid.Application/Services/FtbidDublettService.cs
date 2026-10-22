using Ftpb.Ftbid.Domain.Prosjekter;
using FtbidEntitet = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Matcher kun kombinasjonen av prosjektnavn, prosjektnummer, tiltakstyper og matrikkel.</summary>
public sealed class FtbidDublettService : IFtbidDublettService
{
    /// <inheritdoc />
    public FtbidDublettTreff? Finn(FtbidEntitet prosjekt, IEnumerable<FtbidEntitet> kandidater)
    {
        var treff = kandidater.Where(k =>
                Lik(prosjekt.Prosjektnavn, k.Prosjektnavn) &&
                Lik(prosjekt.Prosjektnummer, k.Prosjektnummer) &&
                prosjekt.Tiltakstyper.Count > 0 && k.Tiltakstyper.Count > 0 &&
                TiltakstypeSett.LagHash(prosjekt.Tiltakstyper) == TiltakstypeSett.LagHash(k.Tiltakstyper) &&
                TiltakstypeSett.Normaliser(prosjekt.Tiltakstyper).SequenceEqual(TiltakstypeSett.Normaliser(k.Tiltakstyper)) &&
                prosjekt.Eiendom.Any(a => k.Eiendom.Any(b =>
                    a.Eiendomsidentifikasjon.Kommunenummer == b.Eiendomsidentifikasjon.Kommunenummer &&
                    a.Eiendomsidentifikasjon.Gaardsnummer == b.Eiendomsidentifikasjon.Gaardsnummer &&
                    a.Eiendomsidentifikasjon.Bruksnummer == b.Eiendomsidentifikasjon.Bruksnummer)))
            .OrderBy(k => k.Ean, StringComparer.Ordinal).FirstOrDefault();
        return treff is null ? null : new(treff.Ean,
            ["Prosjektnavn", "Prosjektnummer", "Tiltakstyper", "Matrikkelopplysninger"]);
    }

    private static bool Lik(string? a, string? b) => TiltakstypeSett.NormaliserTekst(a) is { Length: > 0 } normalisert &&
        normalisert == TiltakstypeSett.NormaliserTekst(b);
}
