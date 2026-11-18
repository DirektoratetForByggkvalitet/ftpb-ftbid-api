using Ftpb.Ftbid.Application.Contracts;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Oppslag og søk i prosjektdata og metadata.</summary>
public interface IFtbidOppslagService
{
    /// <summary>Lister alle poster med fast sidestørrelse og stabil sortering.</summary>
    Task<PagedResult<FtbidResult>> ListAsync(Pagination pagination, CancellationToken cancellationToken = default);
    /// <summary>Henter metadata med full ftbid eller de siste åtte sifrene.</summary>
    Task<IReadOnlyList<FtbidResult>> GetAsync(string ftbid, CancellationToken cancellationToken = default);
    /// <summary>Søker med minst ett filter og returnerer tilknytninger.</summary>
    Task<IReadOnlyList<FtbidResult>> SearchAsync(FtbidSok filter, CancellationToken cancellationToken = default);
}
