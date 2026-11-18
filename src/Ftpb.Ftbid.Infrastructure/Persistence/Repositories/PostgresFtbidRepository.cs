using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Application.Repositories;
using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Domain.Identifikatorer;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Domain.Referansedata;
using Ftpb.Ftbid.Infrastructure.Persistence;
using Ftpb.Ftbid.Domain.Errors.Exceptions;
using Microsoft.EntityFrameworkCore;
using FtbidEntity = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Infrastructure.Persistence.Repositories;

/// <summary>Lagrer ftbid i PostgreSQL og beskytter oppretting mot samtidige duplikater.</summary>
public sealed class PostgresFtbidRepository(FtbidDbContext database, IFtbidDublettService dublettService) : IFtbidRepository
{
    /// <inheritdoc />
    public async Task<PagedResult<FtbidEntity>> ListAsync(Pagination pagination, CancellationToken cancellationToken = default)
    {
        var rows = await WithMetadata().OrderBy(f => f.Ean)
            .Skip(pagination.CalculateOffset()).Take(Pagination.Sidestorrelse + 1).ToListAsync(cancellationToken);
        return PagedResult<FtbidEntity>.Create(rows, pagination.Side);
    }

    /// <inheritdoc />
    public async Task<string> CreateAsync(FtbidEntity prosjekt, string gs1Prefiks, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Transaksjonslåsen gjør oppslag og oppretting atomisk også på tvers av API-instanser.
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7099999)", cancellationToken);
        prosjekt.TiltakstyperHash = TiltakstypeSett.ComputeHash(prosjekt.Tiltakstyper);
        var kommuner = prosjekt.Eiendom.Select(e => e.Eiendomsidentifikasjon.Kommunenummer).Distinct().ToArray();
        var kandidater = await database.Ftbider.AsNoTracking().AsSplitQuery()
            .Include(f => f.Eiendom)
            .Where(f => (f.TiltakstyperHash == prosjekt.TiltakstyperHash || f.TiltakstyperHash == null) &&
                f.Eiendom.Any(e => kommuner.Contains(e.Eiendomsidentifikasjon.Kommunenummer)))
            .ToListAsync(cancellationToken);
        var treff = dublettService.Finn(prosjekt, kandidater);
        if (treff is not null)
            throw new FtbidDublettException(treff.Ftbid, treff.SamsvarendeFelter);
        var sekvens = await database.Database.SqlQueryRaw<long>("SELECT nextval('ftbid_sekvens') AS \"Value\"").SingleAsync(cancellationToken);
        prosjekt.Ean = EanGenerator.Generate(gs1Prefiks, sekvens);
        database.Ftbider.Add(prosjekt);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return prosjekt.Ean;
    }

    /// <inheritdoc />
    public async Task TilknyttAsync(string ftbid, IReadOnlyList<Tilknytning> tilknytninger, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await database.Ftbider.AnyAsync(f => f.Ean == ftbid, cancellationToken))
            throw new FtbidIkkeFunnetException(ftbid);
        // Fast rekkefølge begrenser faren for vranglås ved overlappende lister.
        // Ved gjentatt ReferanseId i listen gjelder den siste oppføringen.
        foreach (var tilknytning in tilknytninger.GroupBy(t => t.ReferanseId, StringComparer.Ordinal).Select(g => g.Last())
                     .OrderBy(t => t.ReferanseId, StringComparer.Ordinal))
        {
            var metadata = tilknytning;
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO skjemametadata (arkivreferanse, skjematype, tiltakstyper)
                VALUES ({metadata.ReferanseId}, {metadata.Typebeskrivelse}, {metadata.Tiltakstyper.ToArray()})
                ON CONFLICT (arkivreferanse) DO UPDATE
                SET skjematype = EXCLUDED.skjematype, tiltakstyper = EXCLUDED.tiltakstyper
                """, cancellationToken);
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO identifikasjonsmetadata (arkivreferanse, ftbid) VALUES ({metadata.ReferanseId}, {ftbid})
                ON CONFLICT (arkivreferanse) DO UPDATE SET ftbid = EXCLUDED.ftbid
                """, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FtbidEntity>> GetAsync(string ftbid, CancellationToken cancellationToken) =>
        await WithMetadata().Where(f => f.Ean == ftbid).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<FtbidEntity>> SearchAsync(FtbidSok filter, CancellationToken cancellationToken) =>
        await BuildQuery(filter).ToListAsync(cancellationToken);

    internal IQueryable<FtbidEntity> BuildQuery(FtbidSok filter)
    {
        var sporring = WithMetadata();
        var kommune = filter.Kommunenummer?.Trim();
        var adresse = filter.Adresse?.Trim().ToLowerInvariant();
        var bygning = filter.Bygningsnummer?.Trim().ToLowerInvariant();
        var bolig = filter.Bolignummer?.Trim().ToLowerInvariant();
        // Alle eiendomsfiltre gjelder samme eiendom, og adressetekst gjelder én adresse på denne.
        if (!string.IsNullOrWhiteSpace(kommune) || filter.Gaardsnummer.HasValue || filter.Bruksnummer.HasValue ||
            filter.Festenummer.HasValue || filter.Seksjonsnummer.HasValue || !string.IsNullOrWhiteSpace(adresse) ||
            !string.IsNullOrWhiteSpace(bygning) || !string.IsNullOrWhiteSpace(bolig))
            sporring = sporring.Where(f => f.Eiendom.Any(e =>
                (string.IsNullOrEmpty(kommune) || e.Eiendomsidentifikasjon.Kommunenummer == kommune) &&
                (!filter.Gaardsnummer.HasValue || e.Eiendomsidentifikasjon.Gaardsnummer == filter.Gaardsnummer) &&
                (!filter.Bruksnummer.HasValue || e.Eiendomsidentifikasjon.Bruksnummer == filter.Bruksnummer) &&
                (!filter.Festenummer.HasValue || e.Eiendomsidentifikasjon.Festenummer == filter.Festenummer) &&
                (!filter.Seksjonsnummer.HasValue || e.Eiendomsidentifikasjon.Seksjonsnummer == filter.Seksjonsnummer) &&
                (string.IsNullOrEmpty(bygning) || (e.Bygningsnummer != null && e.Bygningsnummer.ToLower() == bygning)) &&
                (string.IsNullOrEmpty(bolig) || (e.Bolignummer != null && e.Bolignummer.ToLower() == bolig)) &&
                (string.IsNullOrEmpty(adresse) || e.Adresse.Any(a =>
                    ((a.Adresselinje1 ?? "") + " " + (a.Adresselinje2 ?? "") + " " + (a.Adresselinje3 ?? "") + " " +
                     (a.Postnr ?? "") + " " + (a.Poststed ?? "") + " " + (a.Landkode ?? "") + " " +
                     (a.Gatenavn ?? "") + " " + (a.Husnr ?? "") + " " + (a.Bokstav ?? "")).ToLower().Contains(adresse)))));
        if (!string.IsNullOrWhiteSpace(filter.Prosjektnavn))
        {
            var name = filter.Prosjektnavn.Trim().ToLowerInvariant();
            sporring = sporring.Where(f => f.NormalisertProsjektnavn.Contains(name));
        }
        if (!string.IsNullOrWhiteSpace(filter.Ftbid))
            sporring = sporring.Where(f => f.Ean == filter.Ftbid);
        if (!string.IsNullOrWhiteSpace(filter.Tiltaksnavn))
        {
            var name = filter.Tiltaksnavn.Trim().ToLowerInvariant();
            sporring = sporring.Where(f => f.NormalisertProsjektnavn.Contains(name));
        }
        if (!string.IsNullOrWhiteSpace(filter.Tiltakstype))
        {
            var type = filter.Tiltakstype.Trim().ToLowerInvariant();
            sporring = sporring.Where(f => f.Tiltakstyper.Any(t => t.ToLower() == type) ||
                f.Identifikasjonsmetadata.Any(i => i.Skjemametadata.Tiltakstyper.Any(t => t.ToLower() == type)));
        }
        if (!string.IsNullOrWhiteSpace(filter.EksternProsjektreferanse))
        {
            var value = filter.EksternProsjektreferanse.Trim().ToLowerInvariant();
            sporring = sporring.Where(f => f.EksternProsjektreferanse != null
                ? f.EksternProsjektreferanse.ToLower() == value
                : f.Prosjektnummer != null && f.Prosjektnummer.ToLower() == value);
        }
        if (!string.IsNullOrWhiteSpace(filter.KommunaltSaksnummer))
        {
            var value = filter.KommunaltSaksnummer.Trim().ToLowerInvariant();
            sporring = sporring.Where(f => f.KommunaltSaksnummer != null && f.KommunaltSaksnummer.ToLower() == value);
        }
        if (!string.IsNullOrWhiteSpace(filter.AnsvarligSokerOrganisasjonsnummer))
        {
            var value = filter.AnsvarligSokerOrganisasjonsnummer.Trim().ToLowerInvariant();
            sporring = sporring.Where(f => f.AnsvarligSokerOrganisasjonsnummer != null && f.AnsvarligSokerOrganisasjonsnummer.ToLower() == value);
        }
        return sporring.OrderBy(f => f.Ean);
    }

    private IQueryable<FtbidEntity> WithMetadata() => database.Ftbider.AsNoTracking().AsSplitQuery()
        .Include(f => f.Eiendom).ThenInclude(e => e.Adresse)
        .Include(f => f.Identifikasjonsmetadata).ThenInclude(m => m.Skjemametadata);
}
