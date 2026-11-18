using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Application.Repositories;
using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Domain.Errors.Exceptions;
using FtbidEntity = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;
using Xunit;

namespace Ftpb.Ftbid.Tests.Unit;

public sealed class TilknytningTests
{
    [Theory]
    [InlineData("urn:dibk:bygg:innsending-id:65465411-34184-13213")]
    [InlineData("urn:dibk:bygg:rapport-id:65465411-34184-13213")]
    [InlineData("URN:ISBN:9781234567890")]
    [InlineData("urn:example:a/b%20c")]
    [InlineData("urn:example:a?+resolve?x?=query?y#fragment")]
    [InlineData("urn:example:a#")]
    public async Task GyldigeUrnerSendesUendretTilLager(string referanse)
    {
        var repository = new TestRepository();
        await new FtbidTilknytningService(repository, new()).TilknyttAsync(new() { Ftbid = "709999900000000017", Tilknytninger = [new() { ReferanseId = referanse }] });
        Assert.Equal(referanse, repository.Referanse);
    }

    [Theory]
    [InlineData("AR123")]
    [InlineData("https://example.org/skjema")]
    [InlineData("urn:x:a")]
    [InlineData("urn:-dibk:a")]
    [InlineData("urn:dibk-:a")]
    [InlineData("urn:dibk:")]
    [InlineData("urn:dibk:/a")]
    [InlineData("urn:dibk:æ")]
    [InlineData("urn:dibk:a b")]
    [InlineData("urn:dibk:a\nb")]
    [InlineData("urn:dibk:a%")]
    [InlineData("urn:dibk:a%XZ")]
    [InlineData("urn:dibk:a?query")]
    [InlineData("urn:dibk:a?+")]
    [InlineData("urn:dibk:a?=")]
    public async Task UgyldigUrnAvvisesForLagring(string referanse)
    {
        var repository = new TestRepository();
        var error = await Assert.ThrowsAsync<UgyldigUrnException>(() => new FtbidTilknytningService(repository, new())
            .TilknyttAsync(new() { Ftbid = "709999900000000017", Tilknytninger = [new() { ReferanseId = referanse }] }));
        Assert.Contains("ReferanseId", error.Message);
        Assert.Null(repository.Referanse);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReferanseErPakrevd(string? referanse)
    {
        await Assert.ThrowsAsync<RequiredValueMissingException>(() => new FtbidTilknytningService(new TestRepository(), new())
            .TilknyttAsync(new() { Ftbid = "709999900000000017", Tilknytninger = [new() { ReferanseId = referanse }] }));
    }

    [Theory]
    [InlineData(80)]
    [InlineData(81)]
    public async Task ReferanselengdeValideresMotLagringsgrensen(int lengde)
    {
        var repository = new TestRepository();
        var referanse = "urn:dibk:" + new string('a', lengde - 9);
        var service = new FtbidTilknytningService(repository, new());
        if (lengde == 80)
        {
            await service.TilknyttAsync(new() { Ftbid = "709999900000000017", Tilknytninger = [new() { ReferanseId = referanse }] });
            Assert.Equal(referanse, repository.Referanse);
        }
        else
        {
            await Assert.ThrowsAsync<TextTooLongException>(() => service.TilknyttAsync(new() { Ftbid = "709999900000000017", Tilknytninger = [new() { ReferanseId = referanse }] }));
            Assert.Null(repository.Referanse);
        }
    }

    [Fact]
    public async Task HeleListenValideresForLagring()
    {
        var repository = new TestRepository();
        await Assert.ThrowsAsync<UgyldigUrnException>(() => new FtbidTilknytningService(repository, new()).TilknyttAsync(new()
        {
            Ftbid = "709999900000000017",
            Tilknytninger = [new() { ReferanseId = "urn:dibk:gyldig" }, new() { ReferanseId = "ugyldig" }]
        }));
        Assert.Null(repository.Referanse);
    }

    [Fact]
    public async Task ManglendeOgTomListeAvvises()
    {
        var service = new FtbidTilknytningService(new TestRepository(), new());
        foreach (var liste in new List<TilknytningRequest>?[] { null, [], [null!] })
            await Assert.ThrowsAsync<RequiredValueMissingException>(() => service.TilknyttAsync(new()
            { Ftbid = "709999900000000017", Tilknytninger = liste }));
    }

    [Fact]
    public async Task UgyldigeMetadataAvvisesForLagring()
    {
        var repository = new TestRepository();
        var service = new FtbidTilknytningService(repository, new());
        await Assert.ThrowsAsync<TextTooLongException>(() => service.TilknyttAsync(new()
        {
            Ftbid = "709999900000000017",
            Tilknytninger = [new() { ReferanseId = "urn:dibk:gyldig", Typebeskrivelse = new string('a', 501) }]
        }));
        foreach (var typer in new List<string>[] { null!, [" "] })
            await Assert.ThrowsAsync<UgyldigeSkjemametadataException>(() => service.TilknyttAsync(new()
            {
                Ftbid = "709999900000000017",
                Tilknytninger = [new() { ReferanseId = "urn:dibk:gyldig", Tiltakstyper = typer }]
            }));
        Assert.Null(repository.Referanse);
    }

    private sealed class TestRepository : IFtbidRepository
    {
        public string? Referanse { get; private set; }
        public Task TilknyttAsync(string ftbid, IReadOnlyList<Tilknytning> tilknytninger, CancellationToken cancellationToken) { Referanse = tilknytninger.Single().ReferanseId; return Task.CompletedTask; }
        public Task<string> CreateAsync(FtbidEntity prosjekt, string gs1Prefiks, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<FtbidEntity>> GetAsync(string ftbid, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<PagedResult<FtbidEntity>> IFtbidRepository.ListAsync(Pagination pagination, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<FtbidEntity>> SearchAsync(FtbidSok filter, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
