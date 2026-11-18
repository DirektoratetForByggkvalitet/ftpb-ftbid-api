using Ftpb.Ftbid.Application.Configuration;
using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Application.Repositories;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Domain.Referansedata;
using static Ftpb.Ftbid.Application.Validation.FtbidValidation;
using Ftpb.Ftbid.Domain.Errors.Exceptions;
using FtbidEntity = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Henter og søker i prosjekter og setter sammen resultatmodeller.</summary>
public sealed class FtbidOppslagService(IFtbidRepository repository, FtbidOptions settings) : IFtbidOppslagService
{
    /// <inheritdoc />
    public async Task<PagedResult<FtbidResult>> ListAsync(Pagination pagination, CancellationToken cancellationToken = default)
    {
        var side = await repository.ListAsync(pagination, cancellationToken);
        return new(side.Resultater.Select(ToResult).ToList(), side.Side, side.HarNesteSide);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FtbidResult>> GetAsync(string ftbid, CancellationToken cancellationToken = default) =>
        (await repository.GetAsync(NormalizeFtbid(ftbid, settings), cancellationToken)).Select(ToResult).ToList();

    /// <inheritdoc />
    public async Task<IReadOnlyList<FtbidResult>> SearchAsync(FtbidSok filter, CancellationToken cancellationToken = default)
    {
        if (!filter.HasFilters())
            throw new SøkeparameterManglerException();
        if (filter.Gaardsnummer < 0 || filter.Bruksnummer < 0 || filter.Festenummer < 0 || filter.Seksjonsnummer < 0)
            throw new UgyldigSøkenummerException();
        var kommune = string.IsNullOrWhiteSpace(filter.Kommunenummer) ? null : filter.Kommunenummer.Trim();
        if (kommune is not null && (kommune.Length != 4 || !kommune.All(char.IsAsciiDigit)))
            throw new UgyldigKommunenummerException(kommune);
        filter = filter with { Kommunenummer = kommune,
            Ftbid = string.IsNullOrWhiteSpace(filter.Ftbid) ? null : NormalizeFtbid(filter.Ftbid, settings),
            AnsvarligSokerOrganisasjonsnummer = NormalizeAnsvarligSoker(filter.AnsvarligSokerOrganisasjonsnummer) };
        var results = await repository.SearchAsync(filter, cancellationToken);
        if (results.Count == 0) throw new SøkeresultatIkkeFunnetException();
        return results.Select(ToResult).ToList();
    }

    private static FtbidResult ToResult(FtbidEntity prosjekt)
    {
        return new() { Ftbid = prosjekt.Ean, Prosjektnavn = prosjekt.Prosjektnavn,
            Utstedt = prosjekt.Utstedt,
            Prosjektnummer = prosjekt.Prosjektnummer,
            EksternProsjektreferanse = prosjekt.EksternProsjektreferanse, KommunaltSaksnummer = prosjekt.KommunaltSaksnummer,
            Tiltakshaver = prosjekt.Tiltakshaver, Organisasjonsnummer = prosjekt.Organisasjonsnummer,
            Tiltakstyper = prosjekt.Tiltakstyper, AnsvarligSokerOrganisasjonsnummer = prosjekt.AnsvarligSokerOrganisasjonsnummer,
            Eiendom = prosjekt.Eiendom.OrderBy(e => e.Rekkefolge).Select(EiendomResult.FromEntity).ToList(),
            Tilknytninger = prosjekt.Identifikasjonsmetadata.OrderBy(m => m.Arkivreferanse)
                .Select(m => new Tilknytning(m.Arkivreferanse, m.Skjemametadata.Skjematype, m.Skjemametadata.Tiltakstyper)).ToList() };
    }
}
