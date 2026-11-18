using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Domain.Referansedata;
using Ftpb.Ftbid.Infrastructure.Persistence;
using System.Text.Json;
using Ftpb.Ftbid.Domain.Errors.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Ftpb.Ftbid.Infrastructure.Referansedata;

/// <summary>Importerer referansedata uten å starte en HTTP-tjener.</summary>
public sealed class ReferansedataImportService(FtbidDbContext database) : IReferansedataImportService
{
    /// <summary>Validerer og oppdaterer kommuner og skjemaer atomisk fra en norsk JSON-fil.</summary>
    public async Task ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        Referansedata data;
        try
        {
            await using var fil = File.OpenRead(filePath);
            if (fil.Length == 0) throw new ImportFileMissingOrEmptyException();
            data = await JsonSerializer.DeserializeAsync<Referansedata>(fil,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken)
                ?? throw new ImportFileMissingOrEmptyException();
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new ImportFileMissingOrEmptyException(error);
        }
        catch (JsonException error)
        {
            throw new DeserializationFailedException("referansedata", error);
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
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        foreach (var kommune in data.Kommuner)
        {
            var eksisterende = await database.Kommuner.FindAsync([kommune.Kommunenummer], cancellationToken);
            if (eksisterende is null) database.Kommuner.Add(kommune);
            else database.Entry(eksisterende).CurrentValues.SetValues(kommune);
        }
        foreach (var skjema in data.Skjemaer)
        {
            var eksisterende = await database.Skjemametadata.FindAsync([skjema.Arkivreferanse], cancellationToken);
            if (eksisterende is null) database.Skjemametadata.Add(skjema);
            else database.Entry(eksisterende).CurrentValues.SetValues(skjema);
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
