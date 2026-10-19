using Ftpb.Ftbid.Application.Konfigurasjon;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Lagring;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Domain.Referansedata;
using static Ftpb.Ftbid.Application.Validering.FtbidValidering;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using FtbidEntitet = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Henter og søker i prosjekter og setter sammen resultatmodeller.</summary>
public sealed class FtbidOppslagService(IFtbidLager lager, FtbidInnstillinger innstillinger) : IFtbidOppslagService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<FtbidResultat>> HentAsync(string ftbid, CancellationToken avbrudd = default) =>
        (await lager.HentAsync(NormaliserFtbid(ftbid, innstillinger), avbrudd)).Select(TilResultat).ToList();

    /// <inheritdoc />
    public async Task<IReadOnlyList<FtbidResultat>> SokAsync(FtbidSok filter, CancellationToken avbrudd = default)
    {
        var tekstfiltre = new[] { filter.Kommunenummer, filter.Prosjektnavn, filter.Adresse, filter.Ftbid,
            filter.Tiltaksnavn, filter.Tiltakstype, filter.EksternProsjektreferanse, filter.KommunaltSaksnummer,
            filter.AnsvarligSokerOrganisasjonsnummer, filter.Bygningsnummer, filter.Bolignummer };
        if (tekstfiltre.All(string.IsNullOrWhiteSpace) && filter.Gaardsnummer is null &&
            filter.Bruksnummer is null && filter.Festenummer is null && filter.Seksjonsnummer is null)
            throw new SøkeparameterManglerException();
        if (filter.Gaardsnummer < 0 || filter.Bruksnummer < 0 || filter.Festenummer < 0 || filter.Seksjonsnummer < 0)
            throw new UgyldigSøkenummerException();
        var kommune = string.IsNullOrWhiteSpace(filter.Kommunenummer) ? null : filter.Kommunenummer.Trim();
        if (kommune is not null && (kommune.Length != 4 || !kommune.All(char.IsAsciiDigit)))
            throw new UgyldigKommunenummerException(kommune);
        filter = filter with { Kommunenummer = kommune,
            Ftbid = string.IsNullOrWhiteSpace(filter.Ftbid) ? null : NormaliserFtbid(filter.Ftbid, innstillinger),
            AnsvarligSokerOrganisasjonsnummer = NormaliserAnsvarligSoker(filter.AnsvarligSokerOrganisasjonsnummer) };
        var resultater = await lager.SokAsync(filter, avbrudd);
        if (resultater.Count == 0) throw new SøkeresultatIkkeFunnetException();
        return resultater.Select(TilResultat).ToList();
    }

    private static FtbidResultat TilResultat(FtbidEntitet prosjekt)
    {
        return new() { Ftbid = prosjekt.Ean, Prosjektnavn = prosjekt.Prosjektnavn,
            Prosjektnummer = prosjekt.Prosjektnummer,
            EksternProsjektreferanse = prosjekt.EksternProsjektreferanse, KommunaltSaksnummer = prosjekt.KommunaltSaksnummer,
            Tiltakshaver = prosjekt.Tiltakshaver, Organisasjonsnummer = prosjekt.Organisasjonsnummer,
            Tiltakstype = prosjekt.Tiltakstype, AnsvarligSokerOrganisasjonsnummer = prosjekt.AnsvarligSokerOrganisasjonsnummer,
            Eiendom = prosjekt.Eiendom.OrderBy(e => e.Rekkefolge).Select(e => new EiendomResultat(e.Id,
                e.Eiendomsidentifikasjon, e.Adresse.OrderBy(a => a.Rekkefolge).Select(a => new Adressedata(
                    a.Adresselinje1, a.Adresselinje2, a.Adresselinje3, a.Postnr, a.Poststed, a.Landkode,
                    a.Gatenavn, a.Husnr, a.Bokstav)).ToList(), e.Bygningsnummer, e.Bolignummer, e.Kommunenavn)).ToList(),
            TilknyttedeSkjemaer = prosjekt.Identifikasjonsmetadata.OrderBy(m => m.Arkivreferanse)
                .Select(m => new TilknyttetSkjema(m.Arkivreferanse, m.Skjemametadata.Skjematype, m.Skjemametadata.Tiltakstyper)).ToList() };
    }
}
