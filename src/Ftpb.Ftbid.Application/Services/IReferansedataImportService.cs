namespace Ftpb.Ftbid.Application.Services;

/// <summary>Import av kommuneregister og skjemametadata.</summary>
public interface IReferansedataImportService
{
    /// <summary>Validerer og oppdaterer referansedata atomisk fra en norsk JSON-fil.</summary>
    Task ImporterAsync(string filsti, CancellationToken avbrudd = default);
}
