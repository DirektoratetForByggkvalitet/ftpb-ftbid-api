using Ftpb.Ftbid.Api.Autentisering;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ftpb.Ftbid.Api.Kontrollere;

/// <summary>Endrer eiendommer med identifikasjon og strukturerte adresser.</summary>
[ApiController]
[Route("api/ftbid/{ftbid}/eiendom")]
[Authorize(Policy = MaskinportenAutentisering.Endre)]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public sealed class FtbidEiendomKontroller(IFtbidEiendomService service) : ControllerBase
{
    /// <summary>Legger til en eiendom. Hent prosjektet for å se eiendommens ID.</summary>
    [HttpPost]
    public async Task<IActionResult> LeggTilAsync(string ftbid, [FromBody] Eiendomsdata eiendom, CancellationToken avbrudd)
    {
        await service.LeggTilAsync(ftbid, eiendom, avbrudd);
        return NoContent();
    }

    /// <summary>Erstatter eiendommen og hele adresselisten, med uendret ID.</summary>
    [HttpPut("{eiendomId:long}")]
    public async Task<IActionResult> ErstattAsync(string ftbid, long eiendomId, [FromBody] Eiendomsdata eiendom, CancellationToken avbrudd)
    {
        await service.ErstattAsync(ftbid, eiendomId, eiendom, avbrudd);
        return NoContent();
    }

    /// <summary>Sletter eiendommen hvis den finnes. En ftbid kan ha null eiendommer.</summary>
    [HttpDelete("{eiendomId:long}")]
    public async Task<IActionResult> FjernAsync(string ftbid, long eiendomId, CancellationToken avbrudd)
    {
        await service.FjernAsync(ftbid, eiendomId, avbrudd);
        return NoContent();
    }
}
