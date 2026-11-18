using Ftpb.Ftbid.Application.Contracts;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Oppretting av prosjektidentifikatorer med dublettkontroll.</summary>
public interface IFtbidOpprettingService
{
    /// <summary>Validerer opprettingsdata og returnerer en ny ftbid, eller avviser en dublett.</summary>
    Task<string> CreateAsync(OpprettFtbid request, CancellationToken cancellationToken = default);
}
