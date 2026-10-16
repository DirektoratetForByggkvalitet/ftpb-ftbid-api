using Ftpb.Ftbid.Api.Autentisering;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ftpb.Ftbid.Api.Kontrollere;

/// <summary>Tilbyr ftbid-endepunktene fra adapteren på /api/ftbid.</summary>
[ApiController]
[Route("api/ftbid")]
public sealed class FtbidKontroller(IFtbidOpprettingService oppretting, IFtbidOppslagService oppslag,
    IFtbidTilknytningService tilknytning) : ControllerBase
{
    /// <summary>Oppretter en ftbid etter kontroll av dubletter.</summary>
    [HttpPost]
    [Authorize(Policy = MaskinportenAutentisering.Endre)]
    [EnableRateLimiting("oppretting")]
    [EndpointSummary("Opprett en ftbid")]
    [EndpointDescription("Returnerer en 18-sifret identifikator. Dubletter og sannsynlige dubletter gir 409 med ProblemDetails og Location til eksisterende ftbid.")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<string>> OpprettAsync([FromBody] OpprettFtbid foresporsel, CancellationToken avbrudd) =>
        new JsonResult(await oppretting.OpprettAsync(foresporsel, avbrudd)) { StatusCode = StatusCodes.Status200OK };

    /// <summary>Knytter en eksisterende arkivreferanse til en ftbid.</summary>
    [HttpPost("tilknytt")]
    [Authorize(Policy = MaskinportenAutentisering.Endre)]
    [EndpointSummary("Knytt arkivreferanse til ftbid")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> TilknyttAsync([FromBody] TilknyttFtbid foresporsel, CancellationToken avbrudd)
    {
        await tilknytning.TilknyttAsync(foresporsel, avbrudd);
        return NoContent();
    }

    /// <summary>Søker i prosjektdata med eksakt matrikkeltreff og deltreff på navn og adresser.</summary>
    [HttpGet("sok")]
    [Authorize(Policy = MaskinportenAutentisering.Les)]
    [EndpointSummary("Søk etter ftbid og metadata")]
    [EndpointDescription("Minst ett filter kreves. Flere matrikkelfiltre må treffe samme matrikkelenhet.")]
    [ProducesResponseType<List<FtbidResultat>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<FtbidResultat>>> SokAsync(CancellationToken avbrudd,
        [FromQuery] string? kommunenummer = null, [FromQuery] int? gaardsnummer = null,
        [FromQuery] int? bruksnummer = null, [FromQuery] string? prosjektnavn = null, [FromQuery] string? adresse = null,
        [FromQuery] string? ftid = null,
        [FromQuery] string? tiltaksnavn = null, [FromQuery] string? tiltakstype = null,
        [FromQuery] string? eksternprosjektreferanse = null, [FromQuery] string? kommunaltsaksnummer = null,
        [FromQuery] string? ansvarligsokerorganisasjonsnummer = null, [FromQuery] string? bygningsnummer = null,
        [FromQuery] string? bolignummer = null, [FromQuery] int? festenummer = null,
        [FromQuery] int? seksjonsnummer = null) =>
        Ok(await oppslag.SokAsync(new(kommunenummer, gaardsnummer, bruksnummer, prosjektnavn, adresse,
            ftid, tiltaksnavn, tiltakstype, eksternprosjektreferanse, kommunaltsaksnummer,
            ansvarligsokerorganisasjonsnummer, bygningsnummer, bolignummer, festenummer, seksjonsnummer), avbrudd));

    /// <summary>Henter prosjekt og metadata med full ftbid eller de siste åtte sifrene.</summary>
    [HttpGet("{ftbid}")]
    [Authorize(Policy = MaskinportenAutentisering.Les)]
    [EndpointSummary("Hent metadata for en ftbid")]
    [ProducesResponseType<List<FtbidResultat>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<FtbidResultat>>> HentAsync(string ftbid, CancellationToken avbrudd) =>
        Ok(await oppslag.HentAsync(ftbid, avbrudd));
}
