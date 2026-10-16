using Ftpb.Ftbid.Application.Konfigurasjon;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Lagring;
using Ftpb.Ftbid.Application.Validering;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using static Ftpb.Ftbid.Application.Validering.FtbidValidering;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Validerer og endrer eiendommer uten eksterne oppslag.</summary>
public sealed class FtbidEiendomService(IFtbidEiendomslager lager, IKommuneregister kommuneregister,
    FtbidInnstillinger innstillinger) : IFtbidEiendomService
{
    /// <inheritdoc />
    public async Task LeggTilAsync(string ftbid, Eiendomsdata eiendom, CancellationToken avbrudd = default)
    {
        var id = NormaliserFtbid(ftbid, innstillinger);
        await lager.LeggTilAsync(id, await EiendomValidering.TilEntitetAsync(eiendom, kommuneregister, avbrudd), avbrudd);
    }

    /// <inheritdoc />
    public async Task ErstattAsync(string ftbid, long eiendomId, Eiendomsdata eiendom, CancellationToken avbrudd = default)
    {
        var id = NormaliserFtbid(ftbid, innstillinger);
        if (eiendomId <= 0) throw new UgyldigSøkenummerException();
        await lager.ErstattAsync(id, eiendomId, await EiendomValidering.TilEntitetAsync(eiendom, kommuneregister, avbrudd), avbrudd);
    }

    /// <inheritdoc />
    public Task FjernAsync(string ftbid, long eiendomId, CancellationToken avbrudd = default)
    {
        var id = NormaliserFtbid(ftbid, innstillinger);
        if (eiendomId <= 0) throw new UgyldigSøkenummerException();
        return lager.FjernAsync(id, eiendomId, avbrudd);
    }
}
