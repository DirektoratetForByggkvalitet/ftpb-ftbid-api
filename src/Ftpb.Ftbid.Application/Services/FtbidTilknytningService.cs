using Ftpb.Ftbid.Application.Konfigurasjon;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Lagring;
using static Ftpb.Ftbid.Application.Validering.FtbidValidering;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Knytter arkivreferanser til eksisterende prosjektidentifikatorer.</summary>
public sealed class FtbidTilknytningService(IFtbidLager lager, FtbidInnstillinger innstillinger) : IFtbidTilknytningService
{
    /// <inheritdoc />
    public Task TilknyttAsync(TilknyttFtbid foresporsel, CancellationToken avbrudd = default) =>
        lager.TilknyttAsync(NormaliserFtbid(foresporsel.Ftbid, innstillinger), KrevTekst(foresporsel.Arkivreferanse, "Arkivreferanse", 80), avbrudd);
}
