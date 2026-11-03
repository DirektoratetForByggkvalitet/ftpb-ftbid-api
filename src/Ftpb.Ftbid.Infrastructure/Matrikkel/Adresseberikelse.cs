using Ftpb.Ftbid.Application.Matrikkel;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Infrastructure.Persistens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ftpb.Ftbid.Infrastructure.Matrikkel;

/// <summary>Etterfyller adresser med varig fremdrift og samme transaksjonslås som API-ets eiendomsendringer.</summary>
public sealed class Adresseberikelse(FtbidDbContext database, IMatrikkelAdresseoppslag matrikkel, ILogger<Adresseberikelse> logger)
{
    /// <summary>Behandler én side. Returnerer siste ID, eller null når gjennomgangen er ferdig.</summary>
    public async Task<long?> BehandleBatchAsync(long etterId, int batchstorrelse, int pauseMs,
        Func<bool> fortsett, CancellationToken avbrudd)
    {
        var eiendommer = await database.Set<Eiendom>().AsNoTracking()
            .Where(e => e.Id > etterId && e.MatrikkelAdresserHentet == null)
            .OrderBy(e => e.Id).Take(batchstorrelse).ToArrayAsync(avbrudd);
        var oppslag = new Dictionary<Eiendomsidentifikasjon, IReadOnlyList<Eiendomsadresse>>();
        long? sisteId = null;
        foreach (var eiendom in eiendommer)
        {
            avbrudd.ThrowIfCancellationRequested();
            if (!fortsett()) break;
            sisteId = eiendom.Id;
            try
            {
                var ident = eiendom.Eiendomsidentifikasjon;
                if (!oppslag.TryGetValue(ident, out var adresser))
                {
                    try { adresser = await matrikkel.HentAsync(ident, avbrudd); }
                    finally { await Task.Delay(pauseMs, avbrudd); }
                    oppslag.Add(ident, adresser);
                }
                // Ikke skriv dersom jobben ble deaktivert mens SOAP-oppslaget pågikk.
                if (!fortsett()) break;
                await using var transaksjon = await database.Database.BeginTransactionAsync(avbrudd);
                await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7099999)", avbrudd);
                if (!fortsett()) break;
                var gjeldende = await database.Set<Eiendom>().Include(e => e.Adresse)
                    .SingleOrDefaultAsync(e => e.Id == eiendom.Id, avbrudd);
                if (gjeldende is null || gjeldende.MatrikkelAdresserHentet is not null || gjeldende.Eiendomsidentifikasjon != ident)
                    continue;
                var antall = LeggTilManglende(gjeldende, adresser);
                gjeldende.MatrikkelAdresserHentet = DateTimeOffset.UtcNow;
                await database.SaveChangesAsync(avbrudd);
                await transaksjon.CommitAsync(avbrudd);
                logger.LogInformation("Adresseberikelse: eiendom {EiendomId}, {Treff} adresser fra Matrikkel, {Nye} lagt til",
                    eiendom.Id, adresser.Count, antall);
            }
            catch (OperationCanceledException) when (avbrudd.IsCancellationRequested) { throw; }
            catch (Exception feil)
            {
                logger.LogWarning(feil, "Adresseberikelse feilet for eiendom {EiendomId}; forsøkes igjen i neste gjennomgang", eiendom.Id);
            }
            finally { database.ChangeTracker.Clear(); }
        }
        return sisteId;
    }

    internal static int LeggTilManglende(Eiendom eiendom, IReadOnlyList<Eiendomsadresse> adresser)
    {
        var nokler = eiendom.Adresse.Select(Nokkel).ToHashSet();
        var rekkefolge = eiendom.Adresse.Select(a => a.Rekkefolge).DefaultIfEmpty(-1).Max() + 1;
        var antall = 0;
        foreach (var adresse in adresser)
        {
            if (!nokler.Add(Nokkel(adresse))) continue;
            // Kopier fordi samme oppslag kan brukes av flere eiendomsrader.
            eiendom.Adresse.Add(new Eiendomsadresse
            {
                EiendomId = eiendom.Id, Rekkefolge = rekkefolge++,
                Adresselinje1 = adresse.Adresselinje1, Adresselinje2 = adresse.Adresselinje2,
                Adresselinje3 = adresse.Adresselinje3, Postnr = adresse.Postnr, Poststed = adresse.Poststed,
                Landkode = adresse.Landkode, Gatenavn = adresse.Gatenavn, Husnr = adresse.Husnr, Bokstav = adresse.Bokstav
            });
            antall++;
        }
        return antall;
    }

    private static string? Normaliser(string? verdi) => string.IsNullOrWhiteSpace(verdi) ? null : verdi.Trim().ToUpperInvariant();

    private static object Nokkel(Eiendomsadresse a) => new
    {
        Linje1 = Normaliser(a.Adresselinje1), Linje2 = Normaliser(a.Adresselinje2), Linje3 = Normaliser(a.Adresselinje3),
        Postnr = Normaliser(a.Postnr), Poststed = Normaliser(a.Poststed), Land = Normaliser(a.Landkode),
        Gate = Normaliser(a.Gatenavn), Hus = Normaliser(a.Husnr), Bokstav = Normaliser(a.Bokstav)
    };
}
