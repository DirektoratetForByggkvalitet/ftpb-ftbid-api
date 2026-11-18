using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Azure.Core;
using Ftpb.Ftbid.Domain.Errors.Exceptions;
using Ftpb.Ftbid.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Ftpb.Ftbid.Tests.Unit;

/// <summary>Tester databaseautentisering uten Azure-konto eller nettverkskall.</summary>
public sealed class PostgresConfigurationTests
{
    [Fact]
    public void LokalPassordautentiseringHenterIkkeAzureToken()
    {
        var credential = new TestCredential();
        var options = new DbContextOptionsBuilder<FtbidDbContext>();
        PostgresConfiguration.Configure(options, "Host=localhost;Database=ftbid;Username=ftbid;Password=lokalt", false, credential);
        using var database = new FtbidDbContext(options.Options);
        var connectionString = new NpgsqlConnectionStringBuilder(database.Database.GetConnectionString());
        Assert.Equal("ftbid", connectionString.Username);
        Assert.Equal("lokalt", connectionString.Password);
        Assert.Equal(0, credential.AntallKall);
    }

    [Fact]
    public void EntraIdBrukerTokenidentitetOgGjenbrukerDatakilden()
    {
        var credential = new TestCredential();
        var options = new DbContextOptionsBuilder<FtbidDbContext>();
        PostgresConfiguration.Configure(options, $"Host={Guid.NewGuid():N}.postgres.database.azure.com;Database=ftbid;SSL Mode=VerifyFull",
            true, credential);
        using var forste = new FtbidDbContext(options.Options);
        using var andre = new FtbidDbContext(options.Options);
        var forsteTilkobling = (NpgsqlConnection)forste.Database.GetDbConnection();
        var andreTilkobling = (NpgsqlConnection)andre.Database.GetDbConnection();
        Assert.Equal("utvikler@example.org", forsteTilkobling.UserName);
        Assert.Equal("utvikler@example.org", andreTilkobling.UserName);
        Assert.Equal(1, credential.AntallKall);
        Assert.NotEmpty(credential.Scopes);
    }

    [Theory]
    [InlineData("Host=azure;Database=ftbid;Password=hemmelig;SSL Mode=VerifyFull")]
    [InlineData("Host=azure;Database=ftbid;Passfile=/tmp/hemmelig;SSL Mode=VerifyFull")]
    [InlineData("Host=azure;Database=ftbid")]
    [InlineData("Host=azure;Database=ftbid;SSL Mode=Disable")]
    [InlineData("Host=azure;Database=ftbid;SSL Mode=Allow")]
    [InlineData("Database=ftbid;SSL Mode=VerifyFull")]
    [InlineData("Host=azure;SSL Mode=VerifyFull")]
    public void UgyldigEntraKonfigurasjonAvvisesForTokenHentes(string connectionString)
    {
        var credential = new TestCredential();
        var error = Assert.Throws<MissingConfigurationException>(() =>
            PostgresConfiguration.Configure(new DbContextOptionsBuilder<FtbidDbContext>(), connectionString, true, credential));
        Assert.DoesNotContain("hemmelig", error.Message);
        Assert.Equal(0, credential.AntallKall);
    }

    private sealed class TestCredential : TokenCredential
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
