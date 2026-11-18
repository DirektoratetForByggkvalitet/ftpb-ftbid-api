using Ftpb.Ftbid.Application.Matrikkel;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ftpb.Ftbid.Infrastructure.Matrikkel;

/// <summary>Etterfyller adresser med varig fremdrift og samme transaksjonslås som API-ets eiendomsendringer.</summary>
public sealed class Adresseberikelse(FtbidDbContext database, IMatrikkelAdresseoppslag matrikkel, ILogger<Adresseberikelse> logger)
{
    /// <summary>Behandler én side. Returnerer siste ID, eller null når gjennomgangen er ferdig.</summary>
    public async Task<long?> ProcessBatchAsync(long afterId, int batchSize, int pauseMs,
        Func<bool> shouldContinue, CancellationToken cancellationToken)
    {
        var eiendommer = await database.Set<Eiendom>().AsNoTracking()
            .Where(e => e.Id > afterId && e.MatrikkelAdresserHentet == null)
            .OrderBy(e => e.Id).Take(batchSize).ToArrayAsync(cancellationToken);
        var oppslag = new Dictionary<Eiendomsidentifikasjon, IReadOnlyList<Eiendomsadresse>>();
        long? lastId = null;
        foreach (var eiendom in eiendommer)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!shouldContinue()) break;
            lastId = eiendom.Id;
            try
            {
                var ident = eiendom.Eiendomsidentifikasjon;
                if (!oppslag.TryGetValue(ident, out var adresser))
                {
                    try { adresser = await matrikkel.GetAsync(ident, cancellationToken); }
                    finally { await Task.Delay(pauseMs, cancellationToken); }
                    oppslag.Add(ident, adresser);
                }
                // Ikke skriv dersom jobben ble deaktivert mens SOAP-oppslaget pågikk.
                if (!shouldContinue()) break;
                await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
                await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7099999)", cancellationToken);
                if (!shouldContinue()) break;
                var gjeldende = await database.Set<Eiendom>().Include(e => e.Adresse)
                    .SingleOrDefaultAsync(e => e.Id == eiendom.Id, cancellationToken);
                if (gjeldende is null || gjeldende.MatrikkelAdresserHentet is not null || gjeldende.Eiendomsidentifikasjon != ident)
                    continue;
                var count = LeggTilManglende(gjeldende, adresser);
                gjeldende.MatrikkelAdresserHentet = DateTimeOffset.UtcNow;
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogInformation("Adresseberikelse: eiendom {EiendomId}, {Treff} adresser fra Matrikkel, {Nye} lagt til",
                    eiendom.Id, adresser.Count, count);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                logger.LogWarning(error, "Adresseberikelse feilet for eiendom {EiendomId}; forsøkes igjen i neste gjennomgang", eiendom.Id);
            }
            finally { database.ChangeTracker.Clear(); }
        }
        return lastId;
    }

    internal static int LeggTilManglende(Eiendom eiendom, IReadOnlyList<Eiendomsadresse> adresser)
    {
        var keys = eiendom.Adresse.Select(Nokkel).ToHashSet();
        var rekkefolge = eiendom.Adresse.Select(a => a.Rekkefolge).DefaultIfEmpty(-1).Max() + 1;
        var count = 0;
        foreach (var adresse in adresser)
        {
            if (!keys.Add(Nokkel(adresse))) continue;
            // Kopier fordi samme oppslag kan brukes av flere eiendomsrader.
            eiendom.Adresse.Add(new Eiendomsadresse
            {
                EiendomId = eiendom.Id, Rekkefolge = rekkefolge++,
                Adresselinje1 = adresse.Adresselinje1, Adresselinje2 = adresse.Adresselinje2,
                Adresselinje3 = adresse.Adresselinje3, Postnr = adresse.Postnr, Poststed = adresse.Poststed,
                Landkode = adresse.Landkode, Gatenavn = adresse.Gatenavn, Husnr = adresse.Husnr, Bokstav = adresse.Bokstav
            });
            count++;
        }
        return count;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static object Nokkel(Eiendomsadresse a) => new
    {
        Linje1 = Normalize(a.Adresselinje1), Linje2 = Normalize(a.Adresselinje2), Linje3 = Normalize(a.Adresselinje3),
        Postnr = Normalize(a.Postnr), Poststed = Normalize(a.Poststed), Land = Normalize(a.Landkode),
        Gate = Normalize(a.Gatenavn), Hus = Normalize(a.Husnr), Bokstav = Normalize(a.Bokstav)
    };
}
