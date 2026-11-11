using Ftpb.Ftbid.Application.Konfigurasjon;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Lagring;
using Ftpb.Ftbid.Application.Validering;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using static Ftpb.Ftbid.Application.Validering.FtbidValidering;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Knytter URN-referanser til eksisterende prosjektidentifikatorer.</summary>
public sealed class FtbidTilknytningService(IFtbidLager lager, FtbidInnstillinger innstillinger) : IFtbidTilknytningService
{
    /// <inheritdoc />
    public Task TilknyttAsync(TilknyttFtbid foresporsel, CancellationToken avbrudd = default)
    {
        var ftbid = NormaliserFtbid(foresporsel.Ftbid, innstillinger);
        if (foresporsel.Tilknytninger is not { Count: > 0 })
            throw new PåkrevdVerdiManglerException("Tilknytninger");

        // Valider hele listen før noen metadata eller tilknytninger lagres.
        var tilknytninger = foresporsel.Tilknytninger.Select(tilknytning =>
        {
            if (tilknytning is null)
                throw new PåkrevdVerdiManglerException("Tilknytninger");
            var referanse = UrnValidering.KrevUrn(KrevTekst(tilknytning.ReferanseId, "ReferanseId", 80));
            if (tilknytning.Typebeskrivelse?.Length > 500)
                throw new TekstForLangException("Typebeskrivelse", 500);
            if (tilknytning.Tiltakstyper is null || tilknytning.Tiltakstyper.Any(string.IsNullOrWhiteSpace))
                throw new UgyldigeSkjemametadataException();
            return new Tilknytning(referanse, tilknytning.Typebeskrivelse, tilknytning.Tiltakstyper);
        }).ToList();
        return lager.TilknyttAsync(ftbid, tilknytninger, avbrudd);
    }
}
