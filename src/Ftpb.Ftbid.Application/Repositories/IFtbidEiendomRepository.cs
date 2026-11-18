using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Domain.Prosjekter;

namespace Ftpb.Ftbid.Application.Repositories;

/// <summary>Atomiske endringer av eiendommer på en ftbid.</summary>
public interface IFtbidEiendomRepository
{
    /// <summary>Lister alle poster med fast sidestørrelse og stabil sortering.</summary>
    Task<PagedResult<Eiendom>> ListAsync(Pagination pagination, CancellationToken cancellationToken = default);
    /// <summary>Legger til en eiendom.</summary>
    Task AddAsync(string ftbid, Eiendom eiendom, CancellationToken cancellationToken);
    /// <summary>Erstatter en eiendom og dens adresser, med samme ID.</summary>
    Task ReplaceAsync(string ftbid, long eiendomId, Eiendom eiendom, CancellationToken cancellationToken);
    /// <summary>Fjerner eiendommen hvis den finnes. Også siste eiendom kan fjernes.</summary>
    Task RemoveAsync(string ftbid, long eiendomId, CancellationToken cancellationToken);
}
