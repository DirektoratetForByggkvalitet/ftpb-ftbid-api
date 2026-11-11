using Ftpb.Ftbid.Api.Autentisering;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ftpb.Ftbid.Api.Kontrollere;

/// <summary>Lister og endrer eiendommer med identifikasjon og strukturerte adresser.</summary>
[ApiController]
[Route("api/ftbid")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public sealed class FtbidEiendomKontroller(IFtbidEiendomService service) : ControllerBase
{
    /// <summary>Lister alle eiendommer med tilhørende ftbid.</summary>
    [HttpGet("eiendom")]
    [Authorize(Policy = MaskinportenAutentisering.Les)]
    [EndpointSummary("List alle eiendommer med paginering")]
    [EndpointDescription("Side starter på 1 og er valgfri. Returnerer inntil 50 eiendomsrader sortert på ID, med adresser, tilhørende ftbid og HarNesteSide. Eiendommer på ulike ftbid-er beholdes som separate rader. Sider uten resultater gir 200 med tom liste.")]
    [ProducesResponseType<PaginertResultat<EiendomListeResultat>>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<PaginertResultat<EiendomListeResultat>>> ListeAsync([FromQuery] Paginering paginering,
        CancellationToken avbrudd) => Ok(await service.ListeAsync(paginering, avbrudd));

    /// <summary>Legger til en eiendom. Hent prosjektet for å se eiendommens ID.</summary>
    [HttpPost("{ftbid}/eiendom")]
    [Authorize(Policy = MaskinportenAutentisering.Endre)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LeggTilAsync(string ftbid, [FromBody] Eiendomsdata eiendom, CancellationToken avbrudd)
    {
        await service.LeggTilAsync(ftbid, eiendom, avbrudd);
        return NoContent();
    }

    /// <summary>Erstatter eiendommen og hele adresselisten, med uendret ID.</summary>
    [HttpPut("{ftbid}/eiendom/{eiendomId:long}")]
    [Authorize(Policy = MaskinportenAutentisering.Endre)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ErstattAsync(string ftbid, long eiendomId, [FromBody] Eiendomsdata eiendom, CancellationToken avbrudd)
    {
        await service.ErstattAsync(ftbid, eiendomId, eiendom, avbrudd);
        return NoContent();
    }

    /// <summary>Sletter eiendommen hvis den finnes. En ftbid kan ha null eiendommer.</summary>
    [HttpDelete("{ftbid}/eiendom/{eiendomId:long}")]
    [Authorize(Policy = MaskinportenAutentisering.Endre)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> FjernAsync(string ftbid, long eiendomId, CancellationToken avbrudd)
    {
        await service.FjernAsync(ftbid, eiendomId, avbrudd);
        return NoContent();
    }
}
