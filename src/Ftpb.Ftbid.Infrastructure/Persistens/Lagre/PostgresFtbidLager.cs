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
        prosjekt.TiltakstyperHash = TiltakstypeSett.LagHash(prosjekt.Tiltakstyper);
        var kommuner = prosjekt.Eiendom.Select(e => e.Eiendomsidentifikasjon.Kommunenummer).Distinct().ToArray();
        var kandidater = await database.Ftbider.AsNoTracking().AsSplitQuery()
            .Include(f => f.Eiendom)
            .Where(f => (f.TiltakstyperHash == prosjekt.TiltakstyperHash || f.TiltakstyperHash == null) &&
                f.Eiendom.Any(e => kommuner.Contains(e.Eiendomsidentifikasjon.Kommunenummer)))
            .ToListAsync(avbrudd);
        var treff = dublettService.Finn(prosjekt, kandidater);
        if (treff is not null)
            throw new FtbidDublettException(treff.Ftbid, treff.SamsvarendeFelter);
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
    public async Task<IReadOnlyList<FtbidEntitet>> SokAsync(FtbidSok filter, CancellationToken avbrudd) =>
        await ByggSok(filter).ToListAsync(avbrudd);

    internal IQueryable<FtbidEntitet> ByggSok(FtbidSok filter)
    {
        var sporring = MedMetadata();
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
            var navn = filter.Prosjektnavn.Trim().ToLowerInvariant();
            sporring = sporring.Where(f => f.NormalisertProsjektnavn.Contains(navn));
        }
        if (!string.IsNullOrWhiteSpace(filter.Ftbid))
            sporring = sporring.Where(f => f.Ean == filter.Ftbid);
        if (!string.IsNullOrWhiteSpace(filter.Tiltaksnavn))
        {
            var navn = filter.Tiltaksnavn.Trim().ToLowerInvariant();
            sporring = sporring.Where(f => f.NormalisertProsjektnavn.Contains(navn));
        }
        if (!string.IsNullOrWhiteSpace(filter.Tiltakstype))
        {
            var type = filter.Tiltakstype.Trim().ToLowerInvariant();
            sporring = sporring.Where(f => f.Tiltakstyper.Any(t => t.ToLower() == type) ||
                f.Identifikasjonsmetadata.Any(i => i.Skjemametadata.Tiltakstyper.Any(t => t.ToLower() == type)));
        }
        if (!string.IsNullOrWhiteSpace(filter.EksternProsjektreferanse))
        {
            var verdi = filter.EksternProsjektreferanse.Trim().ToLowerInvariant();
            sporring = sporring.Where(f => f.EksternProsjektreferanse != null
                ? f.EksternProsjektreferanse.ToLower() == verdi
                : f.Prosjektnummer != null && f.Prosjektnummer.ToLower() == verdi);
        }
        if (!string.IsNullOrWhiteSpace(filter.KommunaltSaksnummer))
        {
            var verdi = filter.KommunaltSaksnummer.Trim().ToLowerInvariant();
            sporring = sporring.Where(f => f.KommunaltSaksnummer != null && f.KommunaltSaksnummer.ToLower() == verdi);
        }
        if (!string.IsNullOrWhiteSpace(filter.AnsvarligSokerOrganisasjonsnummer))
        {
            var verdi = filter.AnsvarligSokerOrganisasjonsnummer.Trim().ToLowerInvariant();
            sporring = sporring.Where(f => f.AnsvarligSokerOrganisasjonsnummer != null && f.AnsvarligSokerOrganisasjonsnummer.ToLower() == verdi);
        }
        return sporring.OrderBy(f => f.Ean);
    }

    private IQueryable<FtbidEntitet> MedMetadata() => database.Ftbider.AsNoTracking().AsSplitQuery()
        .Include(f => f.Eiendom).ThenInclude(e => e.Adresse)
        .Include(f => f.Identifikasjonsmetadata).ThenInclude(m => m.Skjemametadata);
}
