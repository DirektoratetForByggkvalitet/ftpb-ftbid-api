using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Matrikkel;

namespace Ftpb.Ftbid.Tests.Oppsett;

/// <summary>Kun testoppsettet bruker denne klienten for de syntetiske PostgreSQL-testdataene.</summary>
public sealed class TestMatrikkelKlient : IMatrikkelKlient
{
    /// <inheritdoc />
    public Task<IReadOnlyList<long>> HentMatrikkelenhetIderAsync(Matrikkeldata matrikkel, CancellationToken avbrudd = default) =>
        Task.FromResult<IReadOnlyList<long>>([12345]);
}
