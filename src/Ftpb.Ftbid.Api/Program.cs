using System;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Api.Konfigurasjon;
using Ftpb.Ftbid.Api.Feilhandtering;
using Ftpb.Ftbid.Api.Helse;
using Ftpb.Ftbid.Application.Feil;
using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Infrastructure.Persistens;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;

var bygger = WebApplication.CreateBuilder(args);
var maskinportenAktivert = bygger.Configuration.GetValue("Maskinporten:Aktivert", true);
bygger.Services.LeggTilFtbidServices(bygger.Configuration, bygger.Environment);
bygger.Services.AddControllers().AddJsonOptions(valg =>
{
    valg.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter<FtIdType>());
    valg.JsonSerializerOptions.PropertyNamingPolicy = null;
    valg.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
bygger.Services.Configure<ApiBehaviorOptions>(valg => valg.InvalidModelStateResponseFactory = kontekst =>
{
    var feil = new DeserialiseringFeiletException("forespørselsmodellen");
    var problem = kontekst.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()
        .CreateProblemDetails(kontekst.HttpContext, statusCode: StatusCodes.Status400BadRequest,
            title: "Ugyldig forespørsel", detail: feil.Message, instance: kontekst.HttpContext.Request.Path);
    problem.Extensions["ftbidFeil"] = FtbidFeil.FraUnntak(feil);
    problem.Extensions["sporingsidentifikator"] = kontekst.HttpContext.TraceIdentifier;
    var resultat = new BadRequestObjectResult(problem);
    resultat.ContentTypes.Add("application/problem+json");
    return resultat;
});
bygger.Services.ConfigureHttpJsonOptions(valg =>
{
    valg.SerializerOptions.Converters.Add(new JsonStringEnumConverter<FtIdType>());
    valg.SerializerOptions.PropertyNamingPolicy = null;
    valg.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
bygger.Services.AddExceptionHandler<FtbidFeilhandtering>();
bygger.Services.AddProblemDetails(valg => valg.CustomizeProblemDetails = kontekst =>
{
    kontekst.ProblemDetails.Instance ??= kontekst.HttpContext.Request.Path;
    kontekst.ProblemDetails.Extensions["sporingsidentifikator"] = kontekst.HttpContext.TraceIdentifier;
    kontekst.ProblemDetails.Title = kontekst.ProblemDetails.Status switch
    {
        400 => "Ugyldig forespørsel", 401 => "Autentisering kreves", 403 => "Tilgang nektet",
        404 => "Endepunktet finnes ikke", 405 => "HTTP-metoden er ikke tillatt", 429 => "For mange forespørsler",
        >= 500 => "Intern serverfeil", _ => kontekst.ProblemDetails.Title
    };
});
bygger.Services.AddHealthChecks().AddCheck<PostgresHelsesjekk>("postgres");
bygger.Services.AddRateLimiter(valg =>
{
    valg.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    valg.AddPolicy("oppretting", kontekst => RateLimitPartition.GetFixedWindowLimiter(
        kontekst.User.FindFirst("organisasjonsnummer")?.Value ?? "ukjent", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 200, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});
bygger.Services.AddEndpointsApiExplorer();
bygger.Services.AddSwaggerGen(valg =>
{
    valg.SwaggerDoc("v1", new OpenApiInfo { Title = "Ftbid-servicen", Version = "v1", Description = "Prosjektidentifikatorer og identifikasjonsmetadata." });
    if (maskinportenAktivert)
    {
        valg.AddSecurityDefinition("Maskinporten", new OpenApiSecurityScheme
        { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", Description = bygger.Environment.IsEnvironment("Local")
            ? "Local krever kun et Bearer-token. Tokenets innhold og scopes valideres ikke."
            : "Tilgangstoken fra Maskinporten med les- eller endrescope." });
        valg.AddSecurityRequirement(dokument => new OpenApiSecurityRequirement
        { [new OpenApiSecuritySchemeReference("Maskinporten", dokument)] = [] });
    }
});
var app = bygger.Build();
if (args.Contains("--migrer", StringComparer.Ordinal) || args.Contains("--importer", StringComparer.Ordinal))
{
    using var omfang = app.Services.CreateScope();
    var database = omfang.ServiceProvider.GetRequiredService<FtbidDbContext>();
    await database.Database.MigrateAsync();
    var plassering = Array.IndexOf(args, "--importer");
    if (plassering >= 0)
    {
        if (plassering + 1 >= args.Length) throw new PåkrevdVerdiManglerException("Filsti etter --importer");
        await omfang.ServiceProvider.GetRequiredService<IReferansedataImportService>()
            .ImporterAsync(args[plassering + 1]);
    }
    return;
}
if (bygger.Configuration.GetValue<bool>("Database:MigrerVedOppstart"))
{
    using var omfang = app.Services.CreateScope();
    await omfang.ServiceProvider.GetRequiredService<FtbidDbContext>().Database.MigrateAsync();
}
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHealthChecks("/helse", new HealthCheckOptions
{
    ResponseWriter = async (kontekst, rapport) => await kontekst.Response.WriteAsJsonAsync(new
    { Status = rapport.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy ? "Frisk" : "Usunn", Beskrivelse = rapport.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy ? "Servicen og PostgreSQL er tilgjengelige." : "Databasen er ikke klar." })
}).AllowAnonymous();
app.MapGet("/helse/levende", () => Results.Ok(new { Status = "Tilgjengelig" })).AllowAnonymous().ExcludeFromDescription();
if (bygger.Configuration.GetValue<bool>("Swagger:Aktivert"))
{
    app.UseSwagger(valg => valg.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(valg => { valg.SwaggerEndpoint("/openapi/v1.json", "Ftbid-servicen v1"); valg.DocumentTitle = "Ftbid-servicen"; });
}
app.Run();

/// <summary>API-ets inngangspunkt, tilgjengelig for integrasjonstester.</summary>
public partial class Program;
