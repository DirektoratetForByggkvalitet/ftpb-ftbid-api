using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Lagring;
using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using FtbidEntitet = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;
using Xunit;

namespace Ftpb.Ftbid.Tests.Enhet;

public sealed class TilknytningTester
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
        var lager = new TestLager();
        await new FtbidTilknytningService(lager, new()).TilknyttAsync(new() { Ftbid = "709999900000000017", Tilknytninger = [new() { ReferanseId = referanse }] });
        Assert.Equal(referanse, lager.Referanse);
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
        var lager = new TestLager();
        var feil = await Assert.ThrowsAsync<UgyldigUrnException>(() => new FtbidTilknytningService(lager, new())
            .TilknyttAsync(new() { Ftbid = "709999900000000017", Tilknytninger = [new() { ReferanseId = referanse }] }));
        Assert.Contains("ReferanseId", feil.Message);
        Assert.Null(lager.Referanse);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReferanseErPakrevd(string? referanse)
    {
        await Assert.ThrowsAsync<PåkrevdVerdiManglerException>(() => new FtbidTilknytningService(new TestLager(), new())
            .TilknyttAsync(new() { Ftbid = "709999900000000017", Tilknytninger = [new() { ReferanseId = referanse }] }));
    }

    [Theory]
    [InlineData(80)]
    [InlineData(81)]
    public async Task ReferanselengdeValideresMotLagringsgrensen(int lengde)
    {
        var lager = new TestLager();
        var referanse = "urn:dibk:" + new string('a', lengde - 9);
        var service = new FtbidTilknytningService(lager, new());
        if (lengde == 80)
        {
            await service.TilknyttAsync(new() { Ftbid = "709999900000000017", Tilknytninger = [new() { ReferanseId = referanse }] });
            Assert.Equal(referanse, lager.Referanse);
        }
        else
        {
            await Assert.ThrowsAsync<TekstForLangException>(() => service.TilknyttAsync(new() { Ftbid = "709999900000000017", Tilknytninger = [new() { ReferanseId = referanse }] }));
            Assert.Null(lager.Referanse);
        }
    }

    [Fact]
    public async Task HeleListenValideresForLagring()
    {
        var lager = new TestLager();
        await Assert.ThrowsAsync<UgyldigUrnException>(() => new FtbidTilknytningService(lager, new()).TilknyttAsync(new()
        {
            Ftbid = "709999900000000017",
            Tilknytninger = [new() { ReferanseId = "urn:dibk:gyldig" }, new() { ReferanseId = "ugyldig" }]
        }));
        Assert.Null(lager.Referanse);
    }

    [Fact]
    public async Task ManglendeOgTomListeAvvises()
    {
        var service = new FtbidTilknytningService(new TestLager(), new());
        foreach (var liste in new List<TilknytningForesporsel>?[] { null, [], [null!] })
            await Assert.ThrowsAsync<PåkrevdVerdiManglerException>(() => service.TilknyttAsync(new()
            { Ftbid = "709999900000000017", Tilknytninger = liste }));
    }

    [Fact]
    public async Task UgyldigeMetadataAvvisesForLagring()
    {
        var lager = new TestLager();
        var service = new FtbidTilknytningService(lager, new());
        await Assert.ThrowsAsync<TekstForLangException>(() => service.TilknyttAsync(new()
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
        Assert.Null(lager.Referanse);
    }

    private sealed class TestLager : IFtbidLager
    {
        public string? Referanse { get; private set; }
        public Task TilknyttAsync(string ftbid, IReadOnlyList<Tilknytning> tilknytninger, CancellationToken avbrudd) { Referanse = tilknytninger.Single().ReferanseId; return Task.CompletedTask; }
        public Task<string> OpprettAsync(FtbidEntitet prosjekt, string gs1Prefiks, CancellationToken avbrudd) => throw new NotSupportedException();
        public Task<IReadOnlyList<FtbidEntitet>> HentAsync(string ftbid, CancellationToken avbrudd) => throw new NotSupportedException();
        Task<PaginertResultat<FtbidEntitet>> IFtbidLager.ListeAsync(Paginering paginering, CancellationToken avbrudd) => throw new NotSupportedException();
        public Task<IReadOnlyList<FtbidEntitet>> SokAsync(FtbidSok filter, CancellationToken avbrudd) => throw new NotSupportedException();
    }
}
