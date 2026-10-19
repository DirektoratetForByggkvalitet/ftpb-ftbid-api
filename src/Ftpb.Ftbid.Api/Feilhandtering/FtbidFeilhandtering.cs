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
    /// <summary>Returnerer dubletter som 409, tomme søk som 404, regelbrudd som 400 og andre interne feil som 500.</summary>
    public async ValueTask<bool> TryHandleAsync(HttpContext kontekst, Exception feil, CancellationToken avbrudd)
    {
        var dublett = feil as FtbidDublettException;
        var valideringsfeil = feil is ValideringsException;
        var ikkeFunnet = feil is SøkeresultatIkkeFunnetException;
        var status = feil switch
        {
            FtbidDublettException => StatusCodes.Status409Conflict,
            SøkeresultatIkkeFunnetException => StatusCodes.Status404NotFound,
            ValideringsException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };
        if (!valideringsfeil && !ikkeFunnet && dublett is null) logger.LogError(feil, "Feil ved behandling av ftbid-forespørsel.");
        var problem = problemfabrikk.CreateProblemDetails(kontekst, statusCode: status,
            title: ikkeFunnet ? "Ingen treff" : dublett is not null ? "Dublett funnet" : valideringsfeil ? "Valideringsfeil" : "Intern serverfeil",
            detail: valideringsfeil || ikkeFunnet || dublett is not null ? feil.Message : "En uventet feil oppstod. Prøv igjen senere.",
            instance: kontekst.Request.Path);
        if (feil is FtbidException ftbidFeil) problem.Extensions["ftbidFeil"] = FtbidFeil.FraUnntak(ftbidFeil);
        if (dublett is not null)
        {
            problem.Extensions["eksisterendeFtbid"] = dublett.Ftbid;
            problem.Extensions["samsvarendeFelter"] = dublett.SamsvarendeFelter;
        }
        problem.Extensions["sporingsidentifikator"] = kontekst.TraceIdentifier;
        kontekst.Response.StatusCode = status;
        await problemservice.WriteAsync(new ProblemDetailsContext { HttpContext = kontekst, ProblemDetails = problem });
        return true;
    }
}
