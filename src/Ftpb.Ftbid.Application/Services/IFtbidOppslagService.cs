using Ftpb.Ftbid.Application.Kontrakter;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Oppslag og søk i prosjektdata og metadata.</summary>
public interface IFtbidOppslagService
{
    /// <summary>Henter metadata med full ftbid eller de siste åtte sifrene.</summary>
    Task<IReadOnlyList<FtbidResultat>> HentAsync(string ftbid, CancellationToken avbrudd = default);
    /// <summary>Søker med minst ett filter og returnerer tilknyttede skjemadata.</summary>
    Task<IReadOnlyList<FtbidResultat>> SokAsync(FtbidSok filter, CancellationToken avbrudd = default);
}
