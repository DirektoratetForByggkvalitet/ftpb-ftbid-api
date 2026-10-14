using Ftpb.Ftbid.Api.Autentisering;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ftpb.Ftbid.Api.Kontrollere;

/// <summary>Legger til og fjerner adresser og matrikkelopplysninger på eksisterende ftbid-er.</summary>
[ApiController]
[Route("api/ftbid/{ftbid}")]
[Authorize(Policy = MaskinportenAutentisering.Endre)]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
public sealed class FtbidEiendomKontroller(IFtbidEiendomService service) : ControllerBase
{
    /// <summary>Legger til en adresse, eller beholder den hvis den allerede finnes.</summary>
    [HttpPost("adresser")]
    [EndpointSummary("Legg til adresse på ftbid")]
    public async Task<IActionResult> LeggTilAdresseAsync(string ftbid, [FromBody] AdresseEndring foresporsel, CancellationToken avbrudd)
    {
        await service.LeggTilAdresseAsync(ftbid, foresporsel.Adresse, avbrudd);
        return NoContent();
    }

    /// <summary>Fjerner adressen og lar neste adresse bli hovedadresse ved behov.</summary>
    [HttpDelete("adresser")]
    [EndpointSummary("Fjern adresse fra ftbid")]
    public async Task<IActionResult> FjernAdresseAsync(string ftbid, [FromQuery] string adresse, CancellationToken avbrudd)
    {
        await service.FjernAdresseAsync(ftbid, adresse, avbrudd);
        return NoContent();
    }

    /// <summary>Legger til en gyldig matrikkelenhet, eller beholder den hvis den allerede finnes.</summary>
    [HttpPost("matrikkelopplysninger")]
    [EndpointSummary("Legg til matrikkelopplysning på ftbid")]
    public async Task<IActionResult> LeggTilMatrikkelAsync(string ftbid, [FromBody] Matrikkeldata foresporsel, CancellationToken avbrudd)
    {
        await service.LeggTilMatrikkelAsync(ftbid, foresporsel, avbrudd);
        return NoContent();
    }

    /// <summary>Fjerner eksakt matrikkelenhet og oppdaterer hovedverdiene, men bevarer minst én matrikkel.</summary>
    [HttpDelete("matrikkelopplysninger")]
    [EndpointSummary("Fjern matrikkelopplysning fra ftbid")]
    [EndpointDescription("Alle fem matrikkelfelter identifiserer enheten. Feste- og seksjonsnummer er 0 hvis utelatt.")]
    public async Task<IActionResult> FjernMatrikkelAsync(string ftbid, CancellationToken avbrudd,
        [FromQuery] string kommunenummer, [FromQuery] int? gaardsnummer, [FromQuery] int? bruksnummer,
        [FromQuery] int festenummer = 0, [FromQuery] int seksjonsnummer = 0)
    {
        if (gaardsnummer is null || bruksnummer is null)
            throw new PåkrevdVerdiManglerException("Gårdsnummer og bruksnummer");
        await service.FjernMatrikkelAsync(ftbid,
            new(kommunenummer, gaardsnummer.Value, bruksnummer.Value, festenummer, seksjonsnummer), avbrudd);
        return NoContent();
    }
}
