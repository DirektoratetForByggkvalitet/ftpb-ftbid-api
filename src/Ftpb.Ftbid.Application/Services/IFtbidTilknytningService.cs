using Ftpb.Ftbid.Application.Kontrakter;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Tilknytning av URN-referanser til skjemaer, rapporter og andre ressurser.</summary>
public interface IFtbidTilknytningService
{
    /// <summary>Knytter en eksisterende ftbid til en eksisterende URN-referanse.</summary>
    Task TilknyttAsync(TilknyttFtbid foresporsel, CancellationToken avbrudd = default);
}
