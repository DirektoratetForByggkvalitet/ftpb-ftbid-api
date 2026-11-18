using Ftpb.Ftbid.Domain.Prosjekter;
using FtbidEntity = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Matcher kun kombinasjonen av prosjektnavn, prosjektnummer, tiltakstyper og matrikkel.</summary>
public sealed class FtbidDublettService : IFtbidDublettService
{
    /// <inheritdoc />
    public FtbidDublettTreff? Finn(FtbidEntity prosjekt, IEnumerable<FtbidEntity> kandidater)
    {
        var treff = kandidater.Where(k =>
                Lik(prosjekt.Prosjektnavn, k.Prosjektnavn) &&
                Lik(prosjekt.Prosjektnummer, k.Prosjektnummer) &&
                prosjekt.Tiltakstyper.Count > 0 && k.Tiltakstyper.Count > 0 &&
                TiltakstypeSett.ComputeHash(prosjekt.Tiltakstyper) == TiltakstypeSett.ComputeHash(k.Tiltakstyper) &&
                TiltakstypeSett.Normalize(prosjekt.Tiltakstyper).SequenceEqual(TiltakstypeSett.Normalize(k.Tiltakstyper)) &&
                prosjekt.Eiendom.Any(a => k.Eiendom.Any(b =>
                    a.Eiendomsidentifikasjon.Kommunenummer == b.Eiendomsidentifikasjon.Kommunenummer &&
                    a.Eiendomsidentifikasjon.Gaardsnummer == b.Eiendomsidentifikasjon.Gaardsnummer &&
                    a.Eiendomsidentifikasjon.Bruksnummer == b.Eiendomsidentifikasjon.Bruksnummer)))
            .OrderBy(k => k.Ean, StringComparer.Ordinal).FirstOrDefault();
        return treff is null ? null : new(treff.Ean,
            ["Prosjektnavn", "Prosjektnummer", "Tiltakstyper", "Matrikkelopplysninger"]);
    }

    private static bool Lik(string? a, string? b) => TiltakstypeSett.NormalizeText(a) is { Length: > 0 } normalized &&
        normalized == TiltakstypeSett.NormalizeText(b);
}
