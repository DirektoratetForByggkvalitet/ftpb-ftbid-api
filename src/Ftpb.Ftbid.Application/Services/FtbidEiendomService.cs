using Ftpb.Ftbid.Application.Configuration;
using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Application.Repositories;
using Ftpb.Ftbid.Application.Validation;
using Ftpb.Ftbid.Domain.Errors.Exceptions;
using static Ftpb.Ftbid.Application.Validation.FtbidValidation;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Validerer og endrer eiendommer uten eksterne oppslag.</summary>
public sealed class FtbidEiendomService(IFtbidEiendomRepository repository, IKommuneregister kommuneregister,
    FtbidOptions settings) : IFtbidEiendomService
{
    /// <inheritdoc />
    public async Task<PagedResult<EiendomListResult>> ListAsync(Pagination pagination, CancellationToken cancellationToken = default)
    {
        var side = await repository.ListAsync(pagination, cancellationToken);
        return new(side.Resultater.Select(e => new EiendomListResult(e.Ftbid, EiendomResult.FromEntity(e))).ToList(),
            side.Side, side.HarNesteSide);
    }

    /// <inheritdoc />
    public async Task AddAsync(string ftbid, Eiendomsdata eiendom, CancellationToken cancellationToken = default)
    {
        var id = NormalizeFtbid(ftbid, settings);
        await repository.AddAsync(id, await EiendomValidation.ToEntityAsync(eiendom, kommuneregister, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReplaceAsync(string ftbid, long eiendomId, Eiendomsdata eiendom, CancellationToken cancellationToken = default)
    {
        var id = NormalizeFtbid(ftbid, settings);
        if (eiendomId <= 0) throw new UgyldigSøkenummerException();
        await repository.ReplaceAsync(id, eiendomId, await EiendomValidation.ToEntityAsync(eiendom, kommuneregister, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string ftbid, long eiendomId, CancellationToken cancellationToken = default)
    {
        var id = NormalizeFtbid(ftbid, settings);
        if (eiendomId <= 0) throw new UgyldigSøkenummerException();
        return repository.RemoveAsync(id, eiendomId, cancellationToken);
    }
}
