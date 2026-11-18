using Ftpb.Ftbid.Application.Configuration;
using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Application.Repositories;
using Ftpb.Ftbid.Application.Validation;
using Ftpb.Ftbid.Domain.Errors.Exceptions;
using static Ftpb.Ftbid.Application.Validation.FtbidValidation;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Knytter URN-referanser til eksisterende prosjektidentifikatorer.</summary>
public sealed class FtbidTilknytningService(IFtbidRepository repository, FtbidOptions settings) : IFtbidTilknytningService
{
    /// <inheritdoc />
    public Task TilknyttAsync(TilknyttFtbid request, CancellationToken cancellationToken = default)
    {
        var ftbid = NormalizeFtbid(request.Ftbid, settings);
        if (request.Tilknytninger is not { Count: > 0 })
            throw new RequiredValueMissingException("Tilknytninger");

        // Valider hele listen før noen metadata eller tilknytninger lagres.
        var tilknytninger = request.Tilknytninger.Select(tilknytning =>
        {
            if (tilknytning is null)
                throw new RequiredValueMissingException("Tilknytninger");
            var referanse = UrnValidation.RequireUrn(RequireText(tilknytning.ReferanseId, "ReferanseId", 80));
            if (tilknytning.Typebeskrivelse?.Length > 500)
                throw new TextTooLongException("Typebeskrivelse", 500);
            if (tilknytning.Tiltakstyper is null || tilknytning.Tiltakstyper.Any(string.IsNullOrWhiteSpace))
                throw new UgyldigeSkjemametadataException();
            return new Tilknytning(referanse, tilknytning.Typebeskrivelse, tilknytning.Tiltakstyper);
        }).ToList();
        return repository.TilknyttAsync(ftbid, tilknytninger, cancellationToken);
    }
}
