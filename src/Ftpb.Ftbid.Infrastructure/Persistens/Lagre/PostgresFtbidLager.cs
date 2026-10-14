using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Lagring;
using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Domain.Identifikatorer;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Domain.Referansedata;
using Ftpb.Ftbid.Infrastructure.Persistens;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using Microsoft.EntityFrameworkCore;
using FtbidEntitet = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Infrastructure.Persistens.Lagre;

/// <summary>Lagrer ftbid i PostgreSQL og beskytter oppretting mot samtidige duplikater.</summary>
public sealed class PostgresFtbidLager(FtbidDbContext database, IFtbidDublettService dublettService) : IFtbidLager
{
    /// <inheritdoc />
    public async Task<string> OpprettAsync(FtbidEntitet prosjekt, string gs1Prefiks, CancellationToken avbrudd)
    {
        await using var transaksjon = await database.Database.BeginTransactionAsync(avbrudd);
        // Transaksjonslåsen gjør oppslag og oppretting atomisk også på tvers av API-instanser.
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7099999)", avbrudd);
        var kommuner = prosjekt.Matrikkelopplysninger.Select(m => m.Kommunenummer).Distinct().ToArray();
        var kandidater = await database.Ftbider.AsNoTracking().AsSplitQuery()
            .Include(f => f.Adresser).Include(f => f.Matrikkelopplysninger)
            .Where(f => kommuner.Contains(f.Kommunenummer) || f.Matrikkelopplysninger.Any(m => kommuner.Contains(m.Kommunenummer)))
            .ToListAsync(avbrudd);
        var treff = dublettService.Finn(prosjekt, kandidater);
        if (treff is not null)
            throw new FtbidDublettException(treff.Ftbid, treff.Sannsynlig, treff.SamsvarendeFelter);
        var sekvens = await database.Database.SqlQueryRaw<long>("SELECT nextval('ftbid_sekvens') AS \"Value\"").SingleAsync(avbrudd);
        prosjekt.Ean = EanGenerator.Lag(gs1Prefiks, sekvens);
        database.Ftbider.Add(prosjekt);
        await database.SaveChangesAsync(avbrudd);
        await transaksjon.CommitAsync(avbrudd);
        return prosjekt.Ean;
    }

    /// <inheritdoc />
    public async Task TilknyttAsync(string ftbid, string arkivreferanse, CancellationToken avbrudd)
    {
        if (!await database.Skjemametadata.AnyAsync(m => m.Arkivreferanse == arkivreferanse, avbrudd))
            throw new ArkivreferanseIkkeFunnetException(arkivreferanse);
        if (!await database.Ftbider.AnyAsync(f => f.Ean == ftbid, avbrudd))
            throw new FtbidIkkeFunnetException(ftbid);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO identifikasjonsmetadata (arkivreferanse, ftbid) VALUES ({arkivreferanse}, {ftbid})
            ON CONFLICT (arkivreferanse) DO UPDATE SET ftbid = EXCLUDED.ftbid
            """, avbrudd);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FtbidEntitet>> HentAsync(string ftbid, CancellationToken avbrudd) =>
        await MedMetadata().Where(f => f.Ean == ftbid).ToListAsync(avbrudd);

    /// <inheritdoc />
    public async Task<IReadOnlyList<FtbidEntitet>> SokAsync(FtbidSok filter, CancellationToken avbrudd)
    {
        var sporring = MedMetadata();
        var kommune = filter.Kommunenummer?.Trim();
        if (!string.IsNullOrWhiteSpace(kommune) || filter.Gaardsnummer.HasValue || filter.Bruksnummer.HasValue)
            sporring = sporring.Where(f => f.Matrikkelopplysninger.Any(m =>
                (string.IsNullOrEmpty(kommune) || m.Kommunenummer == kommune) &&
                (!filter.Gaardsnummer.HasValue || m.Gaardsnummer == filter.Gaardsnummer) &&
                (!filter.Bruksnummer.HasValue || m.Bruksnummer == filter.Bruksnummer)));
        if (!string.IsNullOrWhiteSpace(filter.Prosjektnavn))
        {
            var navn = filter.Prosjektnavn.Trim().ToLowerInvariant();
            sporring = sporring.Where(f => f.NormalisertProsjektnavn.Contains(navn));
        }
        if (!string.IsNullOrWhiteSpace(filter.Adresse))
        {
            var adresse = filter.Adresse.Trim().ToLowerInvariant();
            sporring = sporring.Where(f => f.Adresser.Any(a => a.Adressetekst.ToLower().Contains(adresse)));
        }
        return await sporring.OrderBy(f => f.Ean).ToListAsync(avbrudd);
    }

    private IQueryable<FtbidEntitet> MedMetadata() => database.Ftbider.AsNoTracking().AsSplitQuery()
        .Include(f => f.Adresser).Include(f => f.Matrikkelopplysninger)
        .Include(f => f.Identifikasjonsmetadata).ThenInclude(m => m.Skjemametadata);
}
