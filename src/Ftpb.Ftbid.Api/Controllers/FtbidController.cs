using Ftpb.Ftbid.Api.Authentication;
using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ftpb.Ftbid.Api.Controllers;

/// <summary>Tilbyr ftbid-endepunktene fra adapteren på /api/ftbid.</summary>
[ApiController]
[Route("api/ftbid")]
public sealed class FtbidController(IFtbidOpprettingService oppretting, IFtbidOppslagService oppslag,
    IFtbidTilknytningService tilknytning) : ControllerBase
{
    /// <summary>Oppretter en ftbid etter kontroll av dubletter.</summary>
    [HttpPost]
    [Authorize(Policy = MaskinportenAuthentication.Endre)]
    [EnableRateLimiting("oppretting")]
    [EndpointSummary("Opprett en ftbid")]
    [EndpointDescription("Returnerer en 18-sifret identifikator. Samme prosjektnavn, prosjektnummer, tiltakstyper og matrikkel (kommune, gård, bruk) gir 409 med ProblemDetails og eksisterende ftbid. Alle fire opplysninger kreves.")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<string>> CreateAsync([FromBody] OpprettFtbid request, CancellationToken cancellationToken) =>
        new JsonResult(await oppretting.CreateAsync(request, cancellationToken)) { StatusCode = StatusCodes.Status200OK };

    /// <summary>Knytter en liste av referanser til en ftbid.</summary>
    [HttpPost("{ftbid}/tilknytninger")]
    [Authorize(Policy = MaskinportenAuthentication.Endre)]
    [EndpointSummary("Knytt skjema, rapport eller annen referanse til ftbid")]
    [EndpointDescription("Angi ftbid i URL-en og send en ikke-tom liste Tilknytninger med ReferanseId, Typebeskrivelse og Tiltakstyper. ReferanseId må være en gyldig URN (RFC 8141-syntaks), maksimalt 80 tegn. Metadata registreres eller oppdateres fra forespørselen. Hele listen lagres atomisk. Ugyldig URN gir 400 med ProblemDetails og feilkode V0073. Flere referanser kan knyttes til samme ftbid; eksisterende tilknytninger som ikke er med i listen beholdes. Gjentatt tilknytning er idempotent; en referanse flyttes ved tilknytning til en annen ftbid. Oppslag og søk returnerer Tilknytninger med ReferanseId, Typebeskrivelse og Tiltakstyper.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> TilknyttAsync([FromRoute] string ftbid,
        [FromBody] TilknyttFtbidRequest request, CancellationToken cancellationToken)
    {
        await tilknytning.TilknyttAsync(new TilknyttFtbid
        {
            Ftbid = ftbid,
            Tilknytninger = request.Tilknytninger
        }, cancellationToken);
        return NoContent();
    }

    /// <summary>Lister alle ftbid-er med paginering, eller søker med angitte filtre.</summary>
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [HttpGet]
    [Authorize(Policy = MaskinportenAuthentication.Les)]
    [EndpointSummary("List eller søk etter ftbid og metadata")]
    [EndpointDescription("Uten søkefiltre returneres en paginert liste med inntil 50 resultater, Side, Sidestorrelse og HarNesteSide. Side starter på 1 og er valgfri. Med søkefiltre returneres en vanlig resultatliste som tidligere; side brukes ikke. Flere matrikkelfiltre må treffe samme matrikkelenhet. Søk uten treff gir 404. En listeside uten treff gir 200 med tom Resultater.")]
    [ProducesResponseType<object>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> SearchAsync([FromQuery] Pagination pagination, CancellationToken cancellationToken,
        [FromQuery] string? kommunenummer = null, [FromQuery] int? gaardsnummer = null,
        [FromQuery] int? bruksnummer = null, [FromQuery] string? prosjektnavn = null, [FromQuery] string? adresse = null,
        [FromQuery] string? ftid = null,
        [FromQuery] string? tiltaksnavn = null, [FromQuery] string? tiltakstype = null,
        [FromQuery] string? eksternprosjektreferanse = null, [FromQuery] string? kommunaltsaksnummer = null,
        [FromQuery] string? ansvarligsokerorganisasjonsnummer = null, [FromQuery] string? bygningsnummer = null,
        [FromQuery] string? bolignummer = null, [FromQuery] int? festenummer = null,
        [FromQuery] int? seksjonsnummer = null)
    {
        var filter = new FtbidSok(kommunenummer, gaardsnummer, bruksnummer, prosjektnavn, adresse,
            ftid, tiltaksnavn, tiltakstype, eksternprosjektreferanse, kommunaltsaksnummer,
            ansvarligsokerorganisasjonsnummer, bygningsnummer, bolignummer, festenummer, seksjonsnummer);
        if (!filter.HasFilters())
            return Ok(await oppslag.ListAsync(pagination, cancellationToken));
        return Ok(await oppslag.SearchAsync(filter, cancellationToken));
    }

    /// <summary>Henter prosjekt og metadata med full ftbid eller de siste åtte sifrene.</summary>
    [HttpGet("{ftbid}")]
    [Authorize(Policy = MaskinportenAuthentication.Les)]
    [EndpointSummary("Hent metadata for en ftbid")]
    [ProducesResponseType<List<FtbidResult>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<FtbidResult>>> GetAsync(string ftbid, CancellationToken cancellationToken) =>
        Ok(await oppslag.GetAsync(ftbid, cancellationToken));
}
