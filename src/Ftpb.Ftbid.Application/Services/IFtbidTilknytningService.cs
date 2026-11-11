using Ftpb.Ftbid.Application.Kontrakter;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Tilknytning av URN-referanser til skjemaer, rapporter og andre ressurser.</summary>
public interface IFtbidTilknytningService
{
    /// <summary>Registrerer metadata og knytter en liste av URN-referanser til en eksisterende ftbid.</summary>
    Task TilknyttAsync(TilknyttFtbid foresporsel, CancellationToken avbrudd = default);
}
