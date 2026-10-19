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
        if (foresporsel.Eiendom is null || foresporsel.Eiendom.Count == 0) throw new PåkrevdVerdiManglerException("Eiendom");
        if (foresporsel.Eiendom.Count > 100) throw new ForMangeVerdierException("Eiendom", 100);
        var eiendommer = new List<Eiendom>();
        foreach (var data in foresporsel.Eiendom)
        {
            var eiendom = await EiendomValidering.TilEntitetAsync(data, kommuneregister, avbrudd);
            eiendom.Rekkefolge = eiendommer.Count;
            eiendommer.Add(eiendom);
        }
        var prosjekt = new FtbidEntitet
        {
            Prosjektnavn = navn, NormalisertProsjektnavn = navn.ToLowerInvariant(), System = system,
            Prosjektnummer = KrevTekst(foresporsel.Prosjektnr, "Prosjektnummer", 200),
            EksternProsjektreferanse = ValgfriTekst(foresporsel.EksternProsjektreferanse, "Ekstern prosjektreferanse", 200),
            KommunaltSaksnummer = ValgfriTekst(foresporsel.KommunaltSaksnummer, "Kommunalt saksnummer", 200),
            Tiltakshaver = ValgfriTekst(foresporsel.Tiltakshaver, "Tiltakshaver", 500),
            Organisasjonsnummer = ValgfriTekst(foresporsel.Organisasjonsnummer, "Organisasjonsnummer", 9),
            AnsvarligSokerOrganisasjonsnummer = ansvarligSoker,
            Tiltakstype = KrevTekst(foresporsel.Tiltakstype, "Tiltakstype", 200),
            Utstedt = DateTimeOffset.UtcNow, Eiendom = eiendommer
        };
        return await lager.OpprettAsync(prosjekt, innstillinger.Gs1Prefiks, avbrudd);
    }

    private static string? ValgfriTekst(string? verdi, string felt, int maks) =>
        string.IsNullOrWhiteSpace(verdi) ? null : KrevTekst(verdi, felt, maks);
}
