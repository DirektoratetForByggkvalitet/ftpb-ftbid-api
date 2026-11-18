using Ftpb.Ftbid.Application.Errors;
using Ftpb.Ftbid.Domain.Errors;
using Ftpb.Ftbid.Domain.Errors.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Ftpb.Ftbid.Api.ErrorHandling;

/// <summary>Oversetter unntak med ASP.NET Core-mellomvare og standard ProblemDetails.</summary>
public sealed class FtbidExceptionHandler(ILogger<FtbidExceptionHandler> logger, ProblemDetailsFactory problemDetailsFactory,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    /// <summary>Returnerer dubletter som 409, tomme søk som 404, regelbrudd som 400 og andre interne feil som 500.</summary>
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken cancellationToken)
    {
        var dublett = error as FtbidDublettException;
        var valideringsfeil = error is ValidationException;
        var ikkeFunnet = error is SøkeresultatIkkeFunnetException;
        var status = error switch
        {
            FtbidDublettException => StatusCodes.Status409Conflict,
            SøkeresultatIkkeFunnetException => StatusCodes.Status404NotFound,
            ValidationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };
        if (!valideringsfeil && !ikkeFunnet && dublett is null) logger.LogError(error, "Feil ved behandling av ftbid-forespørsel.");
        var problem = problemDetailsFactory.CreateProblemDetails(context, statusCode: status,
            title: ikkeFunnet ? "Ingen treff" : dublett is not null ? "Dublett funnet" : valideringsfeil ? "Valideringsfeil" : "Intern serverfeil",
            detail: valideringsfeil || ikkeFunnet || dublett is not null ? error.Message : "En uventet feil oppstod. Prøv igjen senere.",
            instance: context.Request.Path);
        if (error is FtbidException ftbidFeil) problem.Extensions["ftbidFeil"] = FtbidError.FromException(ftbidFeil);
        if (dublett is not null)
        {
            problem.Extensions["eksisterendeFtbid"] = dublett.Ftbid;
            problem.Extensions["samsvarendeFelter"] = dublett.SamsvarendeFelter;
        }
        problem.Extensions["sporingsidentifikator"] = context.TraceIdentifier;
        context.Response.StatusCode = status;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
        return true;
    }
}
