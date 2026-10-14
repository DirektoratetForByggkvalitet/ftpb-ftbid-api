using Ftpb.Ftbid.Application.Kontrakter;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Oppretting av prosjektidentifikatorer med dublettkontroll.</summary>
public interface IFtbidOpprettingService
{
    /// <summary>Validerer opprettingsdata og returnerer en ny ftbid, eller avviser en dublett.</summary>
    Task<string> OpprettAsync(OpprettFtbid foresporsel, CancellationToken avbrudd = default);
}
