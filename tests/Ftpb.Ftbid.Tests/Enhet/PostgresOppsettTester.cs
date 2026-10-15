using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Azure.Core;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using Ftpb.Ftbid.Infrastructure.Persistens;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Ftpb.Ftbid.Tests.Enhet;

/// <summary>Tester databaseautentisering uten Azure-konto eller nettverkskall.</summary>
public sealed class PostgresOppsettTester
{
    [Fact]
    public void LokalPassordautentiseringHenterIkkeAzureToken()
    {
        var legitimasjon = new TestLegitimasjon();
        var valg = new DbContextOptionsBuilder<FtbidDbContext>();
        PostgresOppsett.Konfigurer(valg, "Host=localhost;Database=ftbid;Username=ftbid;Password=lokalt", false, legitimasjon);
        using var database = new FtbidDbContext(valg.Options);
        var tilkobling = new NpgsqlConnectionStringBuilder(database.Database.GetConnectionString());
        Assert.Equal("ftbid", tilkobling.Username);
        Assert.Equal("lokalt", tilkobling.Password);
        Assert.Equal(0, legitimasjon.AntallKall);
    }

    [Fact]
    public void EntraIdBrukerTokenidentitetOgGjenbrukerDatakilden()
    {
        var legitimasjon = new TestLegitimasjon();
        var valg = new DbContextOptionsBuilder<FtbidDbContext>();
        PostgresOppsett.Konfigurer(valg, $"Host={Guid.NewGuid():N}.postgres.database.azure.com;Database=ftbid;SSL Mode=VerifyFull",
            true, legitimasjon);
        using var forste = new FtbidDbContext(valg.Options);
        using var andre = new FtbidDbContext(valg.Options);
        var forsteTilkobling = (NpgsqlConnection)forste.Database.GetDbConnection();
        var andreTilkobling = (NpgsqlConnection)andre.Database.GetDbConnection();
        Assert.Equal("utvikler@example.org", forsteTilkobling.UserName);
        Assert.Equal("utvikler@example.org", andreTilkobling.UserName);
        Assert.Equal(1, legitimasjon.AntallKall);
        Assert.NotEmpty(legitimasjon.Scopes);
    }

    [Theory]
    [InlineData("Host=azure;Database=ftbid;Password=hemmelig;SSL Mode=VerifyFull")]
    [InlineData("Host=azure;Database=ftbid;Passfile=/tmp/hemmelig;SSL Mode=VerifyFull")]
    [InlineData("Host=azure;Database=ftbid")]
    [InlineData("Host=azure;Database=ftbid;SSL Mode=Disable")]
    [InlineData("Host=azure;Database=ftbid;SSL Mode=Allow")]
    [InlineData("Database=ftbid;SSL Mode=VerifyFull")]
    [InlineData("Host=azure;SSL Mode=VerifyFull")]
    public void UgyldigEntraKonfigurasjonAvvisesForTokenHentes(string tilkobling)
    {
        var legitimasjon = new TestLegitimasjon();
        var feil = Assert.Throws<KonfigurasjonManglerException>(() =>
            PostgresOppsett.Konfigurer(new DbContextOptionsBuilder<FtbidDbContext>(), tilkobling, true, legitimasjon));
        Assert.DoesNotContain("hemmelig", feil.Message);
        Assert.Equal(0, legitimasjon.AntallKall);
    }

    private sealed class TestLegitimasjon : TokenCredential
    {
        public int AntallKall { get; private set; }
        public string[] Scopes { get; private set; } = [];

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            AntallKall++;
            Scopes = requestContext.Scopes;
            var utlop = DateTimeOffset.UtcNow.AddHours(1);
            var token = new JwtSecurityToken("https://test.invalid/", claims: [new Claim("upn", "utvikler@example.org")],
                expires: utlop.UtcDateTime);
            return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), utlop);
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
