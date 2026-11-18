using System.Text.Json;
using Ftpb.Ftbid.Application.Configuration;
using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Application.Repositories;
using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Domain.Errors.Exceptions;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using FtbidEntity = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;
using Xunit;

namespace Ftpb.Ftbid.Tests.Unit;

/// <summary>Tester den strukturerte eiendomsmodellen uten Docker, nettverk eller database.</summary>
public sealed class FtbidMatrikkelTests
{
    private const string Ftbid = "709999900000000017";
    private static Eiendomsdata Eiendom() => new() { Eiendomsidentifikasjon = new(" 0301 ", 124, 55, 2),
        Adresse = [new(" Admiral veien 2A ", "Hyttefelt 1", Postnr: "0318", Poststed: "Oslo", Landkode: "no",
            Gatenavn: "Admiralveien", Husnr: "02", Bokstav: "A")],
        Bygningsnummer = "300827824", Bolignummer = "H0201", Kommunenavn = "Oslo" };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EiendomKanIkkeUtelatesEllerVaereTom(bool eksplisitt)
    {
        var repository = new TestRepository();
        var prosjekt = new OpprettFtbid { Prosjektnavn = "Uten eiendom", System = "Test", Prosjektnr = "P-1", Tiltakstyper = ["Tilbygg"] };
        if (eksplisitt) prosjekt.Eiendom = [];
        await Assert.ThrowsAsync<RequiredValueMissingException>(() =>
            new FtbidOpprettingService(repository, new Kommuneregister(), new()).CreateAsync(prosjekt));
        Assert.Null(repository.Opprettet);
    }

    [Fact]
    public async Task FlereEiendommerOgStrukturerteAdresserBevaresVedOpprettingOgOppslag()
    {
        var repository = new TestRepository();
        var andre = Eiendom();
        andre.Eiendomsidentifikasjon = new("0301", 9_999_999, 88, 3, 4);
        andre.Adresse = [new("Annen adresse"), new("Adresse nummer to")];
        andre.Bygningsnummer = "000123";
        var prosjekt = new OpprettFtbid { Prosjektnavn = "Test", System = "Test", Prosjektnr = "P-1001", Tiltakstyper = ["Tilbygg"], Eiendom = [Eiendom(), andre] };
        await new FtbidOpprettingService(repository, new Kommuneregister(), new()).CreateAsync(prosjekt);
        repository.Opprettet!.Ean = Ftbid;
        var result = Assert.Single(await new FtbidOppslagService(repository, new()).GetAsync(Ftbid));
        Assert.Equal(2, result.Eiendom.Count);
        Assert.Equal(new Eiendomsidentifikasjon("0301", 124, 55, 2), result.Eiendom[0].Eiendomsidentifikasjon);
        Assert.Equal("0318", result.Eiendom[0].Adresse[0].Postnr);
        Assert.Equal("02", result.Eiendom[0].Adresse[0].Husnr);
        Assert.Equal("NO", result.Eiendom[0].Adresse[0].Landkode);
        Assert.Equal("Admiral veien 2A", result.Eiendom[0].Adresse[0].Adresselinje1);
        Assert.Equal("H0201", result.Eiendom[0].Bolignummer);
        Assert.Equal("Oslo", result.Eiendom[0].Kommunenavn);
        Assert.Equal("000123", result.Eiendom[1].Bygningsnummer);
        Assert.Equal(2, result.Eiendom[1].Adresse.Count);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("\"Matrikkelopplysninger\"", json);
        Assert.DoesNotContain("\"Adresser\"", json);
        Assert.Equal("0318", JsonSerializer.Deserialize<FtbidResult>(json)!.Eiendom[0].Adresse[0].Postnr);
    }

    [Fact]
    public void GammelFlatKontraktAvvisesForAaUnngaaTapAvEiendomsdata()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OpprettFtbid>(
            """{ "Prosjektnavn": "Test", "System": "Test", "Adresser": ["Skal ikke forsvinne"] }"""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Eiendomsdata>(
            """{ "Matrikkelopplysninger": [{ "Kommunenummer": "0301" }] }"""));
    }

    [Fact]
    public async Task EiendomKanHaTomAdresseliste()
    {
        var repository = new TestRepository(); var data = Eiendom(); data.Adresse = [];
        await new FtbidEiendomService(repository, new Kommuneregister(), new()).AddAsync(Ftbid, data);
        Assert.Empty(repository.Tilfoyd!.Adresse);
    }

    [Fact]
    public async Task ErstatningOgSlettingBevarerFtbidOgEiendomsId()
    {
        var repository = new TestRepository(); var service = new FtbidEiendomService(repository, new Kommuneregister(), new());
        await service.ReplaceAsync(Ftbid[^8..], 42, Eiendom());
        Assert.Equal(Ftbid, repository.EndretFtbid);
        Assert.Equal(42, repository.EiendomId);
        await service.RemoveAsync(Ftbid, 42);
        Assert.True(repository.Fjernet);
    }

    [Theory]
    [InlineData("identifikasjon")]
    [InlineData("adresse-null")]
    [InlineData("adresse-tom")]
    [InlineData("adresse-null-element")]
    [InlineData("postnr-for-langt")]
    [InlineData("for-mange-adresser")]
    public async Task UgyldigEiendomAvvisesForLagring(string variant)
    {
        var repository = new TestRepository(); var data = Eiendom();
        if (variant == "identifikasjon") data.Eiendomsidentifikasjon = null;
        if (variant == "adresse-null") data.Adresse = null!;
        if (variant == "adresse-tom") data.Adresse = [new()];
        if (variant == "adresse-null-element") data.Adresse = [null!];
        if (variant == "postnr-for-langt") data.Adresse = [new(Postnr: new string('0', 21))];
        if (variant == "for-mange-adresser") data.Adresse = Enumerable.Repeat(new Adressedata("A"), 101).ToList();
        await Assert.ThrowsAnyAsync<ValidationException>(() => new FtbidEiendomService(repository, new Kommuneregister(), new()).AddAsync(Ftbid, data));
        Assert.Null(repository.Tilfoyd);
    }

    [Fact]
    public async Task UgyldigSekundaereiendomAvviserHeleOpprettingen()
    {
        var repository = new TestRepository(); var andre = Eiendom(); andre.Eiendomsidentifikasjon = new("0301", -1, 1);
        await Assert.ThrowsAsync<UgyldigeMatrikkelopplysningerException>(() => new FtbidOpprettingService(repository, new Kommuneregister(), new())
            .CreateAsync(new() { Prosjektnavn = "Test", System = "Test", Prosjektnr = "P-1001", Tiltakstyper = ["Tilbygg"], Eiendom = [Eiendom(), andre] }));
        Assert.Null(repository.Opprettet);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("9999")]
    public async Task NyeEiendommerKreverGyldigKommune(string kommune)
    {
        var repository = new TestRepository(); var data = Eiendom(); data.Eiendomsidentifikasjon = new(kommune, 1, 1);
        await Assert.ThrowsAsync<UgyldigKommunenummerException>(() => new FtbidEiendomService(repository, new Kommuneregister(), new()).AddAsync(Ftbid, data));
    }

    [Fact]
    public void EfModellenErGyldigOgMatcherMigrasjonene()
    {
        using var database = new FtbidDbContext(new DbContextOptionsBuilder<FtbidDbContext>().UseNpgsql("Host=localhost;Database=modelltest").Options);
        Assert.NotNull(database.Model.FindEntityType(typeof(Ftpb.Ftbid.Domain.Prosjekter.Eiendom)));
        Assert.False(database.Database.HasPendingModelChanges());
    }

    private sealed class Kommuneregister : IKommuneregister
    {
        public Task<bool> ErKommuneGyldigAsync(string kommunenummer, DateTimeOffset tidspunkt, CancellationToken cancellationToken) => Task.FromResult(kommunenummer == "0301");
    }
    private sealed class TestRepository : IFtbidRepository, IFtbidEiendomRepository
    {
        public FtbidEntity? Opprettet { get; private set; }
        public Ftpb.Ftbid.Domain.Prosjekter.Eiendom? Tilfoyd { get; private set; }
        public string? EndretFtbid { get; private set; }
        public long EiendomId { get; private set; }
        public bool Fjernet { get; private set; }
        public Task<string> CreateAsync(FtbidEntity prosjekt, string gs1Prefiks, CancellationToken cancellationToken) { Opprettet = prosjekt; return Task.FromResult(Ftbid); }
        public Task AddAsync(string ftbid, Ftpb.Ftbid.Domain.Prosjekter.Eiendom eiendom, CancellationToken cancellationToken) { EndretFtbid = ftbid; Tilfoyd = eiendom; return Task.CompletedTask; }
        public Task ReplaceAsync(string ftbid, long eiendomId, Ftpb.Ftbid.Domain.Prosjekter.Eiendom eiendom, CancellationToken cancellationToken) { EiendomId = eiendomId; return AddAsync(ftbid, eiendom, cancellationToken); }
        public Task RemoveAsync(string ftbid, long eiendomId, CancellationToken cancellationToken) { EndretFtbid = ftbid; EiendomId = eiendomId; Fjernet = true; return Task.CompletedTask; }
        public Task<IReadOnlyList<FtbidEntity>> GetAsync(string ftbid, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<FtbidEntity>>([Opprettet!]);
        public Task TilknyttAsync(string ftbid, IReadOnlyList<Tilknytning> tilknytninger, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<PagedResult<FtbidEntity>> IFtbidRepository.ListAsync(Pagination pagination, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<PagedResult<Ftpb.Ftbid.Domain.Prosjekter.Eiendom>> IFtbidEiendomRepository.ListAsync(Pagination pagination, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<FtbidEntity>> SearchAsync(FtbidSok filter, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
