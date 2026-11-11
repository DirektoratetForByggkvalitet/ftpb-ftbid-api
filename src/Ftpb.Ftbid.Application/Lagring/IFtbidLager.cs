using Ftpb.Ftbid.Application.Kontrakter;
using FtbidEntitet = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Application.Lagring;

/// <summary>Lagringskontrakt for prosjektidentifikatorer og tilknytninger.</summary>
public interface IFtbidLager
{
    /// <summary>Lister alle poster med fast sidestørrelse og stabil sortering.</summary>
    Task<PaginertResultat<FtbidEntitet>> ListeAsync(Paginering paginering, CancellationToken avbrudd = default);
    /// <summary>Kontrollerer dubletter og oppretter prosjektet atomisk. Dubletter avvises.</summary>
    Task<string> OpprettAsync(FtbidEntitet prosjekt, string gs1Prefiks, CancellationToken avbrudd);
    /// <summary>Lagrer referansemetadata og tilknytter eller flytter referansene atomisk til en ftbid.</summary>
    Task TilknyttAsync(string ftbid, IReadOnlyList<Tilknytning> tilknytninger, CancellationToken avbrudd);
    /// <summary>Henter prosjektet og tilknyttede metadata.</summary>
    Task<IReadOnlyList<FtbidEntitet>> HentAsync(string ftbid, CancellationToken avbrudd);
    /// <summary>Søker i prosjekter, adresser og matrikkelenheter.</summary>
    Task<IReadOnlyList<FtbidEntitet>> SokAsync(FtbidSok filter, CancellationToken avbrudd);
}
