using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Application.Repositories;
using Ftpb.Ftbid.Domain.Errors.Exceptions;
using Ftpb.Ftbid.Domain.Prosjekter;
using Microsoft.EntityFrameworkCore;
using Prosjekt = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Infrastructure.Persistence.Repositories;

/// <summary>Endrer en eiendom og dens adresser atomisk, uten å miste samtidige endringer.</summary>
public sealed class PostgresFtbidEiendomRepository(FtbidDbContext database) : IFtbidEiendomRepository
{
    /// <inheritdoc />
    public async Task<PagedResult<Eiendom>> ListAsync(Pagination pagination, CancellationToken cancellationToken = default)
    {
        var rows = await database.Set<Eiendom>().AsNoTracking().AsSplitQuery().Include(e => e.Adresse).OrderBy(e => e.Id)
            .Skip(pagination.CalculateOffset()).Take(Pagination.Sidestorrelse + 1).ToListAsync(cancellationToken);
        return PagedResult<Eiendom>.Create(rows, pagination.Side);
    }

    /// <inheritdoc />
    public Task AddAsync(string ftbid, Eiendom eiendom, CancellationToken cancellationToken) => UpdateAsync(ftbid, async prosjekt =>
    {
        if (prosjekt.Eiendom.Count >= 100) throw new TooManyValuesException("Eiendom", 100);
        await ValidateIdentityAsync(prosjekt, eiendom, cancellationToken);
        eiendom.Ftbid = ftbid;
        eiendom.Rekkefolge = prosjekt.Eiendom.Select(e => e.Rekkefolge).DefaultIfEmpty(-1).Max() + 1;
        prosjekt.Eiendom.Add(eiendom);
    }, cancellationToken);

    /// <inheritdoc />
    public Task ReplaceAsync(string ftbid, long eiendomId, Eiendom eiendom, CancellationToken cancellationToken) => UpdateAsync(ftbid, async prosjekt =>
    {
        var gammel = prosjekt.Eiendom.SingleOrDefault(e => e.Id == eiendomId)
            ?? throw new EiendomIkkeFunnetException(eiendomId);
        await ValidateIdentityAsync(prosjekt, eiendom, cancellationToken);
        database.RemoveRange(gammel.Adresse);
        gammel.Adresse.Clear();
        // Slett gamle adresseposisjoner før de brukes igjen, innenfor samme transaksjon.
        await database.SaveChangesAsync(cancellationToken);
        gammel.Eiendomsidentifikasjon = eiendom.Eiendomsidentifikasjon;
        gammel.Bygningsnummer = eiendom.Bygningsnummer;
        gammel.Bolignummer = eiendom.Bolignummer;
        gammel.Kommunenavn = eiendom.Kommunenavn;
        gammel.Adresse = eiendom.Adresse;
        gammel.MatrikkelAdresserHentet = null;
    }, cancellationToken);

    /// <inheritdoc />
    public Task RemoveAsync(string ftbid, long eiendomId, CancellationToken cancellationToken) => UpdateAsync(ftbid, prosjekt =>
    {
        var eiendom = prosjekt.Eiendom.SingleOrDefault(e => e.Id == eiendomId);
        if (eiendom is not null) { database.Remove(eiendom); prosjekt.Eiendom.Remove(eiendom); }
        return Task.CompletedTask;
    }, cancellationToken);

    private async Task ValidateIdentityAsync(Prosjekt prosjekt, Eiendom eiendom, CancellationToken cancellationToken)
    {
        var ident = eiendom.Eiendomsidentifikasjon;
        if (await database.Ftbider.AnyAsync(p => p.Ean != prosjekt.Ean && p.NormalisertProsjektnavn == prosjekt.NormalisertProsjektnavn &&
            p.Eiendom.Any(e => e.Eiendomsidentifikasjon.Kommunenummer == ident.Kommunenummer &&
                e.Eiendomsidentifikasjon.Gaardsnummer == ident.Gaardsnummer && e.Eiendomsidentifikasjon.Bruksnummer == ident.Bruksnummer &&
                e.Eiendomsidentifikasjon.Festenummer == ident.Festenummer && e.Eiendomsidentifikasjon.Seksjonsnummer == ident.Seksjonsnummer), cancellationToken))
            throw new ProsjektidentitetAlleredeIBrukException(prosjekt.Ean);
    }

    private async Task UpdateAsync(string ftbid, Func<Prosjekt, Task> endring, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7099999)", cancellationToken);
        var prosjekt = await database.Ftbider.Include(p => p.Eiendom).ThenInclude(e => e.Adresse).AsSplitQuery()
            .SingleOrDefaultAsync(p => p.Ean == ftbid, cancellationToken) ?? throw new FtbidIkkeFunnetException(ftbid);
        await endring(prosjekt);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
