using Ftpb.Ftbid.Application.Konfigurasjon;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Lagring;
using Ftpb.Ftbid.Application.Validering;
using static Ftpb.Ftbid.Application.Validering.FtbidValidering;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Validerer adresse- og matrikkelendringer uten å endre prosjektets identifikator.</summary>
public sealed class FtbidEiendomService(IFtbidEiendomslager lager, IKommuneregister kommuneregister,
    FtbidInnstillinger innstillinger) : IFtbidEiendomService
{
    /// <inheritdoc />
    public Task LeggTilAdresseAsync(string ftbid, string adresse, CancellationToken avbrudd = default) =>
        lager.LeggTilAdresseAsync(NormaliserFtbid(ftbid, innstillinger), KrevTekst(adresse, "Adresse", 1000), avbrudd);

    /// <inheritdoc />
    public Task FjernAdresseAsync(string ftbid, string adresse, CancellationToken avbrudd = default) =>
        lager.FjernAdresseAsync(NormaliserFtbid(ftbid, innstillinger), KrevTekst(adresse, "Adresse", 1000), avbrudd);

    /// <inheritdoc />
    public async Task LeggTilMatrikkelAsync(string ftbid, Matrikkeldata matrikkel, CancellationToken avbrudd = default)
    {
        var identifikator = NormaliserFtbid(ftbid, innstillinger);
        var normalisert = await MatrikkelValidering.ValiderNyAsync(matrikkel, kommuneregister, avbrudd);
        await lager.LeggTilMatrikkelAsync(identifikator, normalisert, avbrudd);
    }

    /// <inheritdoc />
    public Task FjernMatrikkelAsync(string ftbid, Matrikkeldata matrikkel, CancellationToken avbrudd = default) =>
        lager.FjernMatrikkelAsync(NormaliserFtbid(ftbid, innstillinger), MatrikkelValidering.Normaliser(matrikkel), avbrudd);
}
