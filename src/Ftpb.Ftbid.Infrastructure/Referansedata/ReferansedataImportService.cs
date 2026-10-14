using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Domain.Referansedata;
using Ftpb.Ftbid.Infrastructure.Persistens;
using System.Text.Json;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using Microsoft.EntityFrameworkCore;

namespace Ftpb.Ftbid.Infrastructure.Referansedata;

/// <summary>Importerer referansedata uten å starte en HTTP-tjener.</summary>
public sealed class ReferansedataImportService(FtbidDbContext database) : IReferansedataImportService
{
    /// <summary>Validerer og oppdaterer kommuner og skjemaer atomisk fra en norsk JSON-fil.</summary>
    public async Task ImporterAsync(string filsti, CancellationToken avbrudd = default)
    {
        Referansedata data;
        try
        {
            await using var fil = File.OpenRead(filsti);
            if (fil.Length == 0) throw new ImportfilManglerEllerTomException();
            data = await JsonSerializer.DeserializeAsync<Referansedata>(fil,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, avbrudd)
                ?? throw new ImportfilManglerEllerTomException();
        }
        catch (Exception feil) when (feil is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new ImportfilManglerEllerTomException(feil);
        }
        catch (JsonException feil)
        {
            throw new DeserialiseringFeiletException("referansedata", feil);
        }
        if (data.Kommuner is null || data.Skjemaer is null)
            throw new UgyldigeReferansedataException();
        foreach (var kommune in data.Kommuner)
        {
            if (kommune is null || kommune.Kommunenummer is null || kommune.Kommunenummer.Length != 4 ||
                !kommune.Kommunenummer.All(char.IsAsciiDigit))
                throw new UgyldigKommunenummerException(kommune?.Kommunenummer);
            if (kommune.GyldigFra > kommune.GyldigTil) throw new GyldigFraEtterGyldigTilException();
        }
        foreach (var skjema in data.Skjemaer)
        {
            if (skjema is null || string.IsNullOrWhiteSpace(skjema.Arkivreferanse) || skjema.Arkivreferanse.Length > 80 ||
                skjema.Skjematype?.Length > 500 || skjema.Tiltakstyper is null || skjema.Tiltakstyper.Any(string.IsNullOrWhiteSpace))
                throw new UgyldigeSkjemametadataException();
        }
        await using var transaksjon = await database.Database.BeginTransactionAsync(avbrudd);
        foreach (var kommune in data.Kommuner)
        {
            var eksisterende = await database.Kommuner.FindAsync([kommune.Kommunenummer], avbrudd);
            if (eksisterende is null) database.Kommuner.Add(kommune);
            else database.Entry(eksisterende).CurrentValues.SetValues(kommune);
        }
        foreach (var skjema in data.Skjemaer)
        {
            var eksisterende = await database.Skjemametadata.FindAsync([skjema.Arkivreferanse], avbrudd);
            if (eksisterende is null) database.Skjemametadata.Add(skjema);
            else database.Entry(eksisterende).CurrentValues.SetValues(skjema);
        }
        await database.SaveChangesAsync(avbrudd);
        await transaksjon.CommitAsync(avbrudd);
    }
}
