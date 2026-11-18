using Ftpb.Ftbid.Api.Authentication;
using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ftpb.Ftbid.Api.Controllers;

/// <summary>Lister og endrer eiendommer med identifikasjon og strukturerte adresser.</summary>
[ApiController]
[Route("api/ftbid")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public sealed class FtbidEiendomController(IFtbidEiendomService service) : ControllerBase
{
    /// <summary>Lister alle eiendommer med tilhørende ftbid.</summary>
    [HttpGet("eiendom")]
    [Authorize(Policy = MaskinportenAuthentication.Les)]
    [EndpointSummary("List alle eiendommer med paginering")]
    [EndpointDescription("Side starter på 1 og er valgfri. Returnerer inntil 50 eiendomsrader sortert på ID, med adresser, tilhørende ftbid og HarNesteSide. Eiendommer på ulike ftbid-er beholdes som separate rader. Sider uten resultater gir 200 med tom liste.")]
    [ProducesResponseType<PagedResult<EiendomListResult>>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<PagedResult<EiendomListResult>>> ListAsync([FromQuery] Pagination pagination,
        CancellationToken cancellationToken) => Ok(await service.ListAsync(pagination, cancellationToken));

    /// <summary>Legger til en eiendom. Hent prosjektet for å se eiendommens ID.</summary>
    [HttpPost("{ftbid}/eiendom")]
    [Authorize(Policy = MaskinportenAuthentication.Endre)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddAsync(string ftbid, [FromBody] Eiendomsdata eiendom, CancellationToken cancellationToken)
    {
        await service.AddAsync(ftbid, eiendom, cancellationToken);
        return NoContent();
    }

    /// <summary>Erstatter eiendommen og hele adresselisten, med uendret ID.</summary>
    [HttpPut("{ftbid}/eiendom/{eiendomId:long}")]
    [Authorize(Policy = MaskinportenAuthentication.Endre)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReplaceAsync(string ftbid, long eiendomId, [FromBody] Eiendomsdata eiendom, CancellationToken cancellationToken)
    {
        await service.ReplaceAsync(ftbid, eiendomId, eiendom, cancellationToken);
        return NoContent();
    }

    /// <summary>Sletter eiendommen hvis den finnes. En ftbid kan ha null eiendommer.</summary>
    [HttpDelete("{ftbid}/eiendom/{eiendomId:long}")]
    [Authorize(Policy = MaskinportenAuthentication.Endre)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveAsync(string ftbid, long eiendomId, CancellationToken cancellationToken)
    {
        await service.RemoveAsync(ftbid, eiendomId, cancellationToken);
        return NoContent();
    }
}
