using Ftpb.Ftbid.Application.Contracts;
using FtbidEntity = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Application.Repositories;

/// <summary>Lagringskontrakt for prosjektidentifikatorer og tilknytninger.</summary>
public interface IFtbidRepository
{
    /// <summary>Lister alle poster med fast sidestørrelse og stabil sortering.</summary>
    Task<PagedResult<FtbidEntity>> ListAsync(Pagination pagination, CancellationToken cancellationToken = default);
    /// <summary>Kontrollerer dubletter og oppretter prosjektet atomisk. Dubletter avvises.</summary>
    Task<string> CreateAsync(FtbidEntity prosjekt, string gs1Prefiks, CancellationToken cancellationToken);
    /// <summary>Lagrer referansemetadata og tilknytter eller flytter referansene atomisk til en ftbid.</summary>
    Task TilknyttAsync(string ftbid, IReadOnlyList<Tilknytning> tilknytninger, CancellationToken cancellationToken);
    /// <summary>Henter prosjektet og tilknyttede metadata.</summary>
    Task<IReadOnlyList<FtbidEntity>> GetAsync(string ftbid, CancellationToken cancellationToken);
    /// <summary>Søker i prosjekter, adresser og matrikkelenheter.</summary>
    Task<IReadOnlyList<FtbidEntity>> SearchAsync(FtbidSok filter, CancellationToken cancellationToken);
}
