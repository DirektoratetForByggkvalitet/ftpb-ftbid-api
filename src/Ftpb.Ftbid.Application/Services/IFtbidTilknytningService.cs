using Ftpb.Ftbid.Application.Kontrakter;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Tilknytning av arkivreferanser.</summary>
public interface IFtbidTilknytningService
{
    /// <summary>Knytter en eksisterende ftbid til en eksisterende arkivreferanse.</summary>
    Task TilknyttAsync(TilknyttFtbid foresporsel, CancellationToken avbrudd = default);
}
