using Ftpb.Ftbid.Application.Kontrakter;
using FtbidEntitet = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Application.Lagring;

/// <summary>Lagringskontrakt for prosjektidentifikatorer og tilknytninger.</summary>
public interface IFtbidLager
{
    /// <summary>Kontrollerer dubletter og oppretter prosjektet atomisk. Dubletter avvises.</summary>
    Task<string> OpprettAsync(FtbidEntitet prosjekt, string gs1Prefiks, CancellationToken avbrudd);
    /// <summary>Tilknytter en ftbid til en kjent arkivreferanse, eller flytter en eksisterende tilknytning.</summary>
    Task TilknyttAsync(string ftbid, string arkivreferanse, CancellationToken avbrudd);
    /// <summary>Henter prosjektet og tilknyttede metadata.</summary>
    Task<IReadOnlyList<FtbidEntitet>> HentAsync(string ftbid, CancellationToken avbrudd);
    /// <summary>Søker i prosjekter, adresser og matrikkelenheter.</summary>
    Task<IReadOnlyList<FtbidEntitet>> SokAsync(FtbidSok filter, CancellationToken avbrudd);
}
