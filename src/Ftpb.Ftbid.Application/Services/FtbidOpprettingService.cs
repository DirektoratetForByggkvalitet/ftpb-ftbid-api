using Ftpb.Ftbid.Application.Validering;
using Ftpb.Ftbid.Application.Konfigurasjon;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Lagring;
using Ftpb.Ftbid.Domain.Prosjekter;
using static Ftpb.Ftbid.Application.Validering.FtbidValidering;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using FtbidEntitet = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Validerer prosjektdata og oppretter en ftbid etter duplikatkontroll.</summary>
public sealed class FtbidOpprettingService(IFtbidLager lager, IKommuneregister kommuneregister, FtbidInnstillinger innstillinger) : IFtbidOpprettingService
{
    /// <inheritdoc />
    public async Task<string> OpprettAsync(OpprettFtbid foresporsel, CancellationToken avbrudd = default)
    {
        var ansvarligSoker = NormaliserAnsvarligSoker(foresporsel.AnsvarligSokerOrganisasjonsnummer);
        var navn = KrevTekst(foresporsel.Prosjektnavn, "Prosjektnavn", 500);
        var system = KrevTekst(foresporsel.System, "System", 200);
        var matrikler = new List<Matrikkeldata>();
        var harEnkeltfelter = foresporsel.Kommunenummer is not null || foresporsel.Gaardsnummer.HasValue ||
            foresporsel.Bruksnummer.HasValue || foresporsel.Festenummer.HasValue || foresporsel.Seksjonsnummer.HasValue;
        if (harEnkeltfelter || foresporsel.Matrikkelopplysninger is not { Count: > 0 })
        {
            if (foresporsel.Gaardsnummer is null || foresporsel.Bruksnummer is null)
                throw new PåkrevdVerdiManglerException("Gårdsnummer og bruksnummer");
            matrikler.Add(new(KrevTekst(foresporsel.Kommunenummer, "Kommunenummer", 4),
                foresporsel.Gaardsnummer.Value, foresporsel.Bruksnummer.Value,
                foresporsel.Festenummer ?? 0, foresporsel.Seksjonsnummer ?? 0));
        }
        if (foresporsel.Matrikkelopplysninger is not null) matrikler.AddRange(foresporsel.Matrikkelopplysninger);
        if (matrikler.Count > 100) throw new ForMangeVerdierException("Matrikkelopplysninger", 100);
        for (var i = 0; i < matrikler.Count; i++)
            matrikler[i] = await MatrikkelValidering.ValiderNyAsync(matrikler[i], kommuneregister, avbrudd);
        var adresser = new List<string>();
        if (!string.IsNullOrWhiteSpace(foresporsel.Adresse)) adresser.Add(foresporsel.Adresse);
        if (foresporsel.Adresser is not null) adresser.AddRange(foresporsel.Adresser);
        if (adresser.Count > 100) throw new ForMangeVerdierException("Adresser", 100);
        var prosjekt = new FtbidEntitet
        {
            Prosjektnavn = navn, NormalisertProsjektnavn = navn.ToLowerInvariant(), System = system,
            Prosjektnummer = string.IsNullOrWhiteSpace(foresporsel.Prosjektnr) ? null : KrevTekst(foresporsel.Prosjektnr, "Prosjektnummer", 200),
            EksternProsjektreferanse = ValgfriTekst(foresporsel.EksternProsjektreferanse, "Ekstern prosjektreferanse", 200),
            KommunaltSaksnummer = ValgfriTekst(foresporsel.KommunaltSaksnummer, "Kommunalt saksnummer", 200),
            Tiltakshaver = ValgfriTekst(foresporsel.Tiltakshaver, "Tiltakshaver", 500),
            Organisasjonsnummer = ValgfriTekst(foresporsel.Organisasjonsnummer, "Organisasjonsnummer", 9),
            AnsvarligSokerOrganisasjonsnummer = ansvarligSoker,
            Bruksenhetsnummer = ValgfriTekst(foresporsel.Bruksenhetsnummer, "Bruksenhetsnummer", 50),
            Bygningsnummer = ValgfriTekst(foresporsel.Bygningsnummer, "Bygningsnummer", 50),
            Tiltakstype = ValgfriTekst(foresporsel.Tiltakstype, "Tiltakstype", 200),
            Utstedt = DateTimeOffset.UtcNow,
            Adresser = adresser.Select(a => KrevTekst(a, "Adresse", 1000)).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select((a, i) => new Adresse { Adressetekst = a, Rekkefolge = i }).ToList(),
            Matrikkelopplysninger = matrikler.Select(m => m with { Kommunenummer = m.Kommunenummer.Trim() }).Distinct()
                .Select((m, i) => new Matrikkelopplysning { Rekkefolge = i, Kommunenummer = m.Kommunenummer,
                    Gaardsnummer = m.Gaardsnummer, Bruksnummer = m.Bruksnummer,
                    Festenummer = m.Festenummer, Seksjonsnummer = m.Seksjonsnummer }).ToList()
        };
        var hoved = prosjekt.Matrikkelopplysninger[0];
        prosjekt.Kommunenummer = hoved.Kommunenummer;
        prosjekt.Gaardsnummer = hoved.Gaardsnummer;
        prosjekt.Bruksnummer = hoved.Bruksnummer;
        prosjekt.Festenummer = hoved.Festenummer;
        prosjekt.Seksjonsnummer = hoved.Seksjonsnummer;
        prosjekt.Adresse = prosjekt.Adresser.FirstOrDefault()?.Adressetekst;
        return await lager.OpprettAsync(prosjekt, innstillinger.Gs1Prefiks, avbrudd);
    }

    private static string? ValgfriTekst(string? verdi, string felt, int maks) =>
        string.IsNullOrWhiteSpace(verdi) ? null : KrevTekst(verdi, felt, maks);
}
