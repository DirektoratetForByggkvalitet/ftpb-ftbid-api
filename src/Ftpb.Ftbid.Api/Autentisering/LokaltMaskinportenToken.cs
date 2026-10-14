using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Ftpb.Ftbid.Api.Autentisering;

/// <summary>Krever kun et Bearer-token i Local, uten å lese eller validere tokeninnholdet.</summary>
public sealed class LokaltMaskinportenToken(IOptionsMonitor<AuthenticationSchemeOptions> valg,
    ILoggerFactory logger, UrlEncoder koder) : AuthenticationHandler<AuthenticationSchemeOptions>(valg, logger, koder)
{
    /// <summary>Avviser manglende, tom eller feilformet Bearer-header og gir ellers lokal tilgang.</summary>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var verdier = Request.Headers.Authorization;
        if (verdier.Count == 0) return Task.FromResult(AuthenticateResult.NoResult());
        if (verdier.Count != 1 || !AuthenticationHeaderValue.TryParse(verdier[0], out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter) || header.Parameter.Any(char.IsWhiteSpace))
            return Task.FromResult(AuthenticateResult.Fail("Et ikke-tomt Bearer-token kreves i Local."));

        // Ingen claims fra det uvaliderte tokenet brukes som identitet, scopes eller organisasjonsnummer.
        var identitet = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "lokal-bruker")], Scheme.Name);
        var billett = new AuthenticationTicket(new ClaimsPrincipal(identitet), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(billett));
    }
}
