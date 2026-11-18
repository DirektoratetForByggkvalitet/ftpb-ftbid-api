using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Ftpb.Ftbid.Api.Authentication;

/// <summary>Krever kun et Bearer-token i Local og Development, uten å lese eller validere tokeninnholdet.</summary>
public sealed class LocalMaskinportenAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>Avviser manglende, tom eller feilformet Bearer-header og gir ellers lokal tilgang.</summary>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var values = Request.Headers.Authorization;
        if (values.Count == 0) return Task.FromResult(AuthenticateResult.NoResult());
        if (values.Count != 1 || !AuthenticationHeaderValue.TryParse(values[0], out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter) || header.Parameter.Any(char.IsWhiteSpace))
            return Task.FromResult(AuthenticateResult.Fail("Et ikke-tomt Bearer-token kreves i Local og Development."));

        // Ingen claims fra det uvaliderte tokenet brukes som identitet, scopes eller organisasjonsnummer.
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "lokal-bruker")], Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
