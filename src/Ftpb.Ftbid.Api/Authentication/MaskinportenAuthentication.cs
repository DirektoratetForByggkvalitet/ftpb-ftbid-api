using System.Security.Claims;
using Ftpb.Ftbid.Domain.Errors.Exceptions;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Ftpb.Ftbid.Api.Authentication;

/// <summary>Registrerer Maskinporten-validering etter mønsteret i NIREG.</summary>
public static class MaskinportenAuthentication
{
    public const string Scheme = "Maskinporten";
    public const string Les = "FtbidLes";
    public const string Endre = "FtbidEndre";

    /// <summary>Registrerer tokenvalidering og scopekrav når aktivert, i Local og Development kun tokenets tilstedeværelse, ellers åpne tilgangsregler.</summary>
    public static IServiceCollection AddMaskinporten(this IServiceCollection services, MaskinportenOptions settings, IHostEnvironment environment)
    {
        if (!settings.Enabled)
        {
            services.AddAuthentication();
            services.AddAuthorizationBuilder()
                .AddPolicy(Les, regel => regel.RequireAssertion(_ => true))
                .AddPolicy(Endre, regel => regel.RequireAssertion(_ => true));
            return services;
        }
        if (environment.IsEnvironment("Local") || environment.IsDevelopment())
        {
            services.AddAuthentication(Scheme)
                .AddScheme<AuthenticationSchemeOptions, LocalMaskinportenAuthenticationHandler>(Scheme, _ => { });
            services.AddAuthorizationBuilder()
                .AddPolicy(Les, regel => regel.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser())
                .AddPolicy(Endre, regel => regel.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser());
            return services;
        }
        if (!Uri.TryCreate(settings.Issuer, UriKind.Absolute, out var utsteder) || utsteder.Scheme != "https" ||
            !settings.Issuer.EndsWith('/') || string.IsNullOrWhiteSpace(settings.ReadScope) ||
            string.IsNullOrWhiteSpace(settings.WriteScope))
            throw new InvalidMaskinportenConfigurationException();
        services.AddAuthentication(Scheme).AddJwtBearer(Scheme, options =>
        {
            options.MapInboundClaims = false;
            options.MetadataAddress = settings.Issuer + ".well-known/oauth-authorization-server";
            options.RequireHttpsMetadata = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = settings.Issuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(settings.Audience), ValidAudience = settings.Audience,
                ValidateIssuerSigningKey = true, RequireSignedTokens = true,
                ValidateLifetime = true, RequireExpirationTime = true, ClockSkew = TimeSpan.FromSeconds(30),
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSha384, SecurityAlgorithms.RsaSha512]
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var orgnummer = GetOrganisasjonsnummer(context.Principal!);
                    if (orgnummer is null) context.Fail("Tokenet mangler et gyldig konsumentorganisasjonsnummer.");
                    else context.Principal!.AddIdentity(new ClaimsIdentity([new Claim("organisasjonsnummer", orgnummer)]));
                    return Task.CompletedTask;
                }
            };
        });
        services.AddAuthorizationBuilder()
            .AddPolicy(Les, regel => regel.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser()
                .RequireAssertion(k => HasScope(k.User, settings.ReadScope)))
            .AddPolicy(Endre, regel => regel.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser()
                .RequireAssertion(k => HasScope(k.User, settings.WriteScope)));
        return services;
    }

    /// <summary>Kontrollerer hele scopeverdier i ett eller flere scope-claims.</summary>
    public static bool HasScope(ClaimsPrincipal bruker, string scope) => bruker.FindAll("scope")
        .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Contains(scope, StringComparer.Ordinal);

    /// <summary>Leser et norsk organisasjonsnummer fra Maskinportens consumer-claim.</summary>
    public static string? GetOrganisasjonsnummer(ClaimsPrincipal bruker)
    {
        var value = bruker.FindFirstValue("consumer");
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("ID", out var id) || id.ValueKind != JsonValueKind.String) return null;
            var identifikator = id.GetString();
            if (identifikator is null || !identifikator.StartsWith("0192:", StringComparison.Ordinal)) return null;
            var nummer = identifikator[5..];
            return nummer.Length == 9 && nummer.All(char.IsAsciiDigit) ? nummer : null;
        }
        catch (JsonException) { return null; }
    }
}
