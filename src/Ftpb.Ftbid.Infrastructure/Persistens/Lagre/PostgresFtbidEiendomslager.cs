using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Lagring;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using Ftpb.Ftbid.Domain.Prosjekter;
using Microsoft.EntityFrameworkCore;
using FtbidEntitet = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Infrastructure.Persistens.Lagre;

/// <summary>Endrer adresser og matrikkelenheter atomisk i PostgreSQL.</summary>
public sealed class PostgresFtbidEiendomslager(FtbidDbContext database) : IFtbidEiendomslager
{
    /// <inheritdoc />
    public Task LeggTilAdresseAsync(string ftbid, string adresse, CancellationToken avbrudd) =>
        EndreAsync(ftbid, prosjekt =>
        {
            if (prosjekt.Adresser.Any(a => string.Equals(a.Adressetekst, adresse, StringComparison.OrdinalIgnoreCase)))
                return Task.CompletedTask;
            if (prosjekt.Adresser.Count >= 100) throw new ForMangeVerdierException("Adresser", 100);
            prosjekt.Adresser.Add(new Adresse { Ftbid = ftbid, Adressetekst = adresse,
                Rekkefolge = prosjekt.Adresser.Select(a => a.Rekkefolge).DefaultIfEmpty(-1).Max() + 1 });
            prosjekt.Adresse = prosjekt.Adresser.MinBy(a => a.Rekkefolge)!.Adressetekst;
            return Task.CompletedTask;
        }, avbrudd);

    /// <inheritdoc />
    public Task FjernAdresseAsync(string ftbid, string adresse, CancellationToken avbrudd) =>
        EndreAsync(ftbid, prosjekt =>
        {
            foreach (var verdi in prosjekt.Adresser.Where(a =>
                string.Equals(a.Adressetekst, adresse, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                database.Remove(verdi);
                prosjekt.Adresser.Remove(verdi);
            }
            prosjekt.Adresse = prosjekt.Adresser.MinBy(a => a.Rekkefolge)?.Adressetekst;
            return Task.CompletedTask;
        }, avbrudd);

    /// <inheritdoc />
    public Task LeggTilMatrikkelAsync(string ftbid, Matrikkeldata matrikkel, CancellationToken avbrudd) =>
        EndreAsync(ftbid, prosjekt =>
        {
            if (prosjekt.Matrikkelopplysninger.Any(m => ErSammeMatrikkel(m, matrikkel))) return Task.CompletedTask;
            if (prosjekt.Matrikkelopplysninger.Count >= 100)
                throw new ForMangeVerdierException("Matrikkelopplysninger", 100);
            prosjekt.Matrikkelopplysninger.Add(new Matrikkelopplysning { Ftbid = ftbid,
                Kommunenummer = matrikkel.Kommunenummer, Gaardsnummer = matrikkel.Gaardsnummer,
                Bruksnummer = matrikkel.Bruksnummer, Festenummer = matrikkel.Festenummer,
                Seksjonsnummer = matrikkel.Seksjonsnummer,
                Rekkefolge = prosjekt.Matrikkelopplysninger.Max(m => m.Rekkefolge) + 1 });
            return Task.CompletedTask;
        }, avbrudd);

    /// <inheritdoc />
    public Task FjernMatrikkelAsync(string ftbid, Matrikkeldata matrikkel, CancellationToken avbrudd) =>
        EndreAsync(ftbid, async prosjekt =>
        {
            var verdier = prosjekt.Matrikkelopplysninger.Where(m => ErSammeMatrikkel(m, matrikkel)).ToList();
            if (verdier.Count == 0) return;
            if (verdier.Count == prosjekt.Matrikkelopplysninger.Count)
                throw new SisteMatrikkelKanIkkeFjernesException(ftbid);
            foreach (var verdi in verdier)
            {
                database.Remove(verdi);
                prosjekt.Matrikkelopplysninger.Remove(verdi);
            }
            var hoved = prosjekt.Matrikkelopplysninger.MinBy(m => m.Rekkefolge)!;
            if (await database.Ftbider.AnyAsync(f => f.Ean != ftbid &&
                f.Kommunenummer == hoved.Kommunenummer && f.Gaardsnummer == hoved.Gaardsnummer &&
                f.Bruksnummer == hoved.Bruksnummer && f.Festenummer == hoved.Festenummer &&
                f.Seksjonsnummer == hoved.Seksjonsnummer && f.NormalisertProsjektnavn == prosjekt.NormalisertProsjektnavn, avbrudd))
                throw new ProsjektidentitetAlleredeIBrukException(ftbid);
            prosjekt.Kommunenummer = hoved.Kommunenummer;
            prosjekt.Gaardsnummer = hoved.Gaardsnummer;
            prosjekt.Bruksnummer = hoved.Bruksnummer;
            prosjekt.Festenummer = hoved.Festenummer;
            prosjekt.Seksjonsnummer = hoved.Seksjonsnummer;
        }, avbrudd);

    private async Task EndreAsync(string ftbid, Func<FtbidEntitet, Task> endring, CancellationToken avbrudd)
    {
        await using var transaksjon = await database.Database.BeginTransactionAsync(avbrudd);
        // Samme lås som ved oppretting beskytter også endringer av den unike prosjektidentiteten.
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7099999)", avbrudd);
        var prosjekt = await database.Ftbider.Include(f => f.Adresser).Include(f => f.Matrikkelopplysninger)
            .AsSplitQuery().SingleOrDefaultAsync(f => f.Ean == ftbid, avbrudd)
            ?? throw new FtbidIkkeFunnetException(ftbid);
        await endring(prosjekt);
        await database.SaveChangesAsync(avbrudd);
        await transaksjon.CommitAsync(avbrudd);
    }

    private static bool ErSammeMatrikkel(Matrikkelopplysning verdi, Matrikkeldata matrikkel) =>
        verdi.Kommunenummer == matrikkel.Kommunenummer && verdi.Gaardsnummer == matrikkel.Gaardsnummer &&
        verdi.Bruksnummer == matrikkel.Bruksnummer && verdi.Festenummer == matrikkel.Festenummer &&
        verdi.Seksjonsnummer == matrikkel.Seksjonsnummer;
}
