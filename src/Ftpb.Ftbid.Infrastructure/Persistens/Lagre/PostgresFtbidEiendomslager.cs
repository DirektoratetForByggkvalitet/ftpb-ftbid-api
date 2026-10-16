using Ftpb.Ftbid.Application.Lagring;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using Ftpb.Ftbid.Domain.Prosjekter;
using Microsoft.EntityFrameworkCore;
using Prosjekt = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Infrastructure.Persistens.Lagre;

/// <summary>Endrer en eiendom og dens adresser atomisk, uten å miste samtidige endringer.</summary>
public sealed class PostgresFtbidEiendomslager(FtbidDbContext database) : IFtbidEiendomslager
{
    /// <inheritdoc />
    public Task LeggTilAsync(string ftbid, Eiendom eiendom, CancellationToken avbrudd) => EndreAsync(ftbid, async prosjekt =>
    {
        if (prosjekt.Eiendom.Count >= 100) throw new ForMangeVerdierException("Eiendom", 100);
        await KontrollerIdentitetAsync(prosjekt, eiendom, avbrudd);
        eiendom.Ftbid = ftbid;
        eiendom.Rekkefolge = prosjekt.Eiendom.Select(e => e.Rekkefolge).DefaultIfEmpty(-1).Max() + 1;
        prosjekt.Eiendom.Add(eiendom);
    }, avbrudd);

    /// <inheritdoc />
    public Task ErstattAsync(string ftbid, long eiendomId, Eiendom eiendom, CancellationToken avbrudd) => EndreAsync(ftbid, async prosjekt =>
    {
        var gammel = prosjekt.Eiendom.SingleOrDefault(e => e.Id == eiendomId)
            ?? throw new EiendomIkkeFunnetException(eiendomId);
        await KontrollerIdentitetAsync(prosjekt, eiendom, avbrudd);
        database.RemoveRange(gammel.Adresse);
        gammel.Adresse.Clear();
        // Slett gamle adresseposisjoner før de brukes igjen, innenfor samme transaksjon.
        await database.SaveChangesAsync(avbrudd);
        gammel.Eiendomsidentifikasjon = eiendom.Eiendomsidentifikasjon;
        gammel.Bygningsnummer = eiendom.Bygningsnummer;
        gammel.Bolignummer = eiendom.Bolignummer;
        gammel.Kommunenavn = eiendom.Kommunenavn;
        gammel.Adresse = eiendom.Adresse;
    }, avbrudd);

    /// <inheritdoc />
    public Task FjernAsync(string ftbid, long eiendomId, CancellationToken avbrudd) => EndreAsync(ftbid, prosjekt =>
    {
        var eiendom = prosjekt.Eiendom.SingleOrDefault(e => e.Id == eiendomId);
        if (eiendom is not null) { database.Remove(eiendom); prosjekt.Eiendom.Remove(eiendom); }
        return Task.CompletedTask;
    }, avbrudd);

    private async Task KontrollerIdentitetAsync(Prosjekt prosjekt, Eiendom eiendom, CancellationToken avbrudd)
    {
        var ident = eiendom.Eiendomsidentifikasjon;
        if (await database.Ftbider.AnyAsync(p => p.Ean != prosjekt.Ean && p.NormalisertProsjektnavn == prosjekt.NormalisertProsjektnavn &&
            p.Eiendom.Any(e => e.Eiendomsidentifikasjon.Kommunenummer == ident.Kommunenummer &&
                e.Eiendomsidentifikasjon.Gaardsnummer == ident.Gaardsnummer && e.Eiendomsidentifikasjon.Bruksnummer == ident.Bruksnummer &&
                e.Eiendomsidentifikasjon.Festenummer == ident.Festenummer && e.Eiendomsidentifikasjon.Seksjonsnummer == ident.Seksjonsnummer), avbrudd))
            throw new ProsjektidentitetAlleredeIBrukException(prosjekt.Ean);
    }

    private async Task EndreAsync(string ftbid, Func<Prosjekt, Task> endring, CancellationToken avbrudd)
    {
        await using var transaksjon = await database.Database.BeginTransactionAsync(avbrudd);
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7099999)", avbrudd);
        var prosjekt = await database.Ftbider.Include(p => p.Eiendom).ThenInclude(e => e.Adresse).AsSplitQuery()
            .SingleOrDefaultAsync(p => p.Ean == ftbid, avbrudd) ?? throw new FtbidIkkeFunnetException(ftbid);
        await endring(prosjekt);
        await database.SaveChangesAsync(avbrudd);
        await transaksjon.CommitAsync(avbrudd);
    }
}
