using Ftpb.Ftbid.Application.Konfigurasjon;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Lagring;
using Ftpb.Ftbid.Application.Validering;
using static Ftpb.Ftbid.Application.Validering.FtbidValidering;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Knytter URN-referanser til eksisterende prosjektidentifikatorer.</summary>
public sealed class FtbidTilknytningService(IFtbidLager lager, FtbidInnstillinger innstillinger) : IFtbidTilknytningService
{
    /// <inheritdoc />
    public Task TilknyttAsync(TilknyttFtbid foresporsel, CancellationToken avbrudd = default) =>
        lager.TilknyttAsync(NormaliserFtbid(foresporsel.Ftbid, innstillinger),
            UrnValidering.KrevUrn(KrevTekst(foresporsel.ReferanseId, "ReferanseId", 80)), avbrudd);
}
