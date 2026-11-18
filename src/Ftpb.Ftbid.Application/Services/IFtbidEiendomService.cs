using Ftpb.Ftbid.Application.Contracts;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Endrer eiendommer med strukturerte adresser og identifikasjon.</summary>
public interface IFtbidEiendomService
{
    /// <summary>Lister alle poster med fast sidestørrelse og stabil sortering.</summary>
    Task<PagedResult<EiendomListResult>> ListAsync(Pagination pagination, CancellationToken cancellationToken = default);
    /// <summary>Legger til en eiendom.</summary>
    Task AddAsync(string ftbid, Eiendomsdata eiendom, CancellationToken cancellationToken = default);
    /// <summary>Erstatter eiendommen med angitt ID.</summary>
    Task ReplaceAsync(string ftbid, long eiendomId, Eiendomsdata eiendom, CancellationToken cancellationToken = default);
    /// <summary>Fjerner eiendommen hvis den finnes, også når den er den siste.</summary>
    Task RemoveAsync(string ftbid, long eiendomId, CancellationToken cancellationToken = default);
}
