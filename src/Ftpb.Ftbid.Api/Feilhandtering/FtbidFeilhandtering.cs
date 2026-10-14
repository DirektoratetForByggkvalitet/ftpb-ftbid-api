using Ftpb.Ftbid.Application.Feil;
using Ftpb.Ftbid.Domain.Feil;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Ftpb.Ftbid.Api.Feilhandtering;

/// <summary>Oversetter unntak med ASP.NET Core-mellomvare og standard ProblemDetails.</summary>
public sealed class FtbidFeilhandtering(ILogger<FtbidFeilhandtering> logger, ProblemDetailsFactory problemfabrikk,
    IProblemDetailsService problemservice) : IExceptionHandler
{
    /// <summary>Returnerer dubletter som 409, regelbrudd som 400, integrasjonsfeil som 502/503 og andre interne feil som 500.</summary>
    public async ValueTask<bool> TryHandleAsync(HttpContext kontekst, Exception feil, CancellationToken avbrudd)
    {
        var dublett = feil as FtbidDublettException;
        var valideringsfeil = feil is ValideringsException;
        var status = feil switch
        {
            FtbidDublettException => StatusCodes.Status409Conflict,
            ValideringsException => StatusCodes.Status400BadRequest,
            UgyldigMatrikkelsvarException => StatusCodes.Status502BadGateway,
            MatrikkelOppslagFeiletException or MatrikkelAutentiseringFeiletException => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };
        if (!valideringsfeil && dublett is null) logger.LogError(feil, "Feil ved behandling av ftbid-forespørsel.");
        var problem = problemfabrikk.CreateProblemDetails(kontekst, statusCode: status,
            title: dublett is not null ? "Dublett funnet" : valideringsfeil ? "Valideringsfeil" : "Intern serverfeil",
            detail: valideringsfeil || dublett is not null ? feil.Message : "En uventet feil oppstod. Prøv igjen senere.",
            instance: kontekst.Request.Path);
        if (feil is FtbidException ftbidFeil) problem.Extensions["ftbidFeil"] = FtbidFeil.FraUnntak(ftbidFeil);
        if (dublett is not null)
        {
            var location = $"{kontekst.Request.PathBase}/api/ftbid/{Uri.EscapeDataString(dublett.Ftbid)}";
            kontekst.Response.Headers.Location = location;
            problem.Extensions["eksisterendeFtbid"] = dublett.Ftbid;
            problem.Extensions["sannsynligDublett"] = dublett.Sannsynlig;
            problem.Extensions["samsvarendeFelter"] = dublett.SamsvarendeFelter;
        }
        problem.Extensions["sporingsidentifikator"] = kontekst.TraceIdentifier;
        kontekst.Response.StatusCode = status;
        await problemservice.WriteAsync(new ProblemDetailsContext { HttpContext = kontekst, ProblemDetails = problem });
        return true;
    }
}
