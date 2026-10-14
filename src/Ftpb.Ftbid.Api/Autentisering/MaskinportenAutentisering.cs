using System.Security.Claims;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Ftpb.Ftbid.Api.Autentisering;

/// <summary>Registrerer Maskinporten-validering etter mønsteret i NIREG.</summary>
public static class MaskinportenAutentisering
{
    public const string Ordning = "Maskinporten";
    public const string Les = "FtbidLes";
    public const string Endre = "FtbidEndre";

    /// <summary>Registrerer tokenvalidering og scopekrav når aktivert, lokalt kun tokenets tilstedeværelse, ellers åpne tilgangsregler.</summary>
    public static IServiceCollection LeggTilMaskinporten(this IServiceCollection services, MaskinportenInnstillinger innstillinger, IHostEnvironment miljo)
    {
        if (!innstillinger.Aktivert)
        {
            services.AddAuthentication();
            services.AddAuthorizationBuilder()
                .AddPolicy(Les, regel => regel.RequireAssertion(_ => true))
                .AddPolicy(Endre, regel => regel.RequireAssertion(_ => true));
            return services;
        }
        if (miljo.IsEnvironment("Local"))
        {
            services.AddAuthentication(Ordning)
                .AddScheme<AuthenticationSchemeOptions, LokaltMaskinportenToken>(Ordning, _ => { });
            services.AddAuthorizationBuilder()
                .AddPolicy(Les, regel => regel.AddAuthenticationSchemes(Ordning).RequireAuthenticatedUser())
                .AddPolicy(Endre, regel => regel.AddAuthenticationSchemes(Ordning).RequireAuthenticatedUser());
            return services;
        }
        if (!Uri.TryCreate(innstillinger.Utsteder, UriKind.Absolute, out var utsteder) || utsteder.Scheme != "https" ||
            !innstillinger.Utsteder.EndsWith('/') || string.IsNullOrWhiteSpace(innstillinger.LesScope) ||
            string.IsNullOrWhiteSpace(innstillinger.EndreScope))
            throw new UgyldigMaskinportenkonfigurasjonException();
        services.AddAuthentication(Ordning).AddJwtBearer(Ordning, alternativer =>
        {
            alternativer.MapInboundClaims = false;
            alternativer.MetadataAddress = innstillinger.Utsteder + ".well-known/oauth-authorization-server";
            alternativer.RequireHttpsMetadata = true;
            alternativer.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = innstillinger.Utsteder,
                ValidateAudience = !string.IsNullOrWhiteSpace(innstillinger.Publikum), ValidAudience = innstillinger.Publikum,
                ValidateIssuerSigningKey = true, RequireSignedTokens = true,
                ValidateLifetime = true, RequireExpirationTime = true, ClockSkew = TimeSpan.FromSeconds(30),
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSha384, SecurityAlgorithms.RsaSha512]
            };
            alternativer.Events = new JwtBearerEvents
            {
                OnTokenValidated = kontekst =>
                {
                    var orgnummer = HentOrganisasjonsnummer(kontekst.Principal!);
                    if (orgnummer is null) kontekst.Fail("Tokenet mangler et gyldig konsumentorganisasjonsnummer.");
                    else kontekst.Principal!.AddIdentity(new ClaimsIdentity([new Claim("organisasjonsnummer", orgnummer)]));
                    return Task.CompletedTask;
                }
            };
        });
        services.AddAuthorizationBuilder()
            .AddPolicy(Les, regel => regel.AddAuthenticationSchemes(Ordning).RequireAuthenticatedUser()
                .RequireAssertion(k => HarScope(k.User, innstillinger.LesScope)))
            .AddPolicy(Endre, regel => regel.AddAuthenticationSchemes(Ordning).RequireAuthenticatedUser()
                .RequireAssertion(k => HarScope(k.User, innstillinger.EndreScope)));
        return services;
    }

    /// <summary>Kontrollerer hele scopeverdier i ett eller flere scope-claims.</summary>
    public static bool HarScope(ClaimsPrincipal bruker, string scope) => bruker.FindAll("scope")
        .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Contains(scope, StringComparer.Ordinal);

    /// <summary>Leser et norsk organisasjonsnummer fra Maskinportens consumer-claim.</summary>
    public static string? HentOrganisasjonsnummer(ClaimsPrincipal bruker)
    {
        var verdi = bruker.FindFirstValue("consumer");
        if (string.IsNullOrWhiteSpace(verdi)) return null;
        try
        {
            using var dokument = JsonDocument.Parse(verdi);
            if (dokument.RootElement.ValueKind != JsonValueKind.Object ||
                !dokument.RootElement.TryGetProperty("ID", out var id) || id.ValueKind != JsonValueKind.String) return null;
            var identifikator = id.GetString();
            if (identifikator is null || !identifikator.StartsWith("0192:", StringComparison.Ordinal)) return null;
            var nummer = identifikator[5..];
            return nummer.Length == 9 && nummer.All(char.IsAsciiDigit) ? nummer : null;
        }
        catch (JsonException) { return null; }
    }
}
