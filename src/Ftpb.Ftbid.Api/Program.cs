using System;
using Ftpb.Ftbid.Api.Configuration;
using Ftpb.Ftbid.Api.ErrorHandling;
using Ftpb.Ftbid.Api.HealthChecks;
using Ftpb.Ftbid.Application.Errors;
using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Infrastructure.Persistence;
using System.Threading.RateLimiting;
using Ftpb.Ftbid.Domain.Errors.Exceptions;
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
using Serilog;

var builder = WebApplication.CreateBuilder(args);
if (builder.Configuration.GetValue("ElasticApm:Enabled", true))
    builder.Services.AddAllElasticApm();
var serilogEnabled = builder.Configuration.GetValue("Serilog:Enabled", true);
if (serilogEnabled)
    builder.Host.UseSerilog((context, logging) =>
        SerilogConfiguration.ConfigureLogging(logging, context.Configuration, "logs-ftbid-backend"));
var maskinportenEnabled = builder.Configuration.GetValue("Maskinporten:Aktivert", true);
builder.Services.AddFtbidServices(builder.Configuration, builder.Environment);
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
    JsonResponseConfiguration.IncludeAllValues(options.JsonSerializerOptions);
});
builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
{
    var error = new DeserializationFailedException("forespørselsmodellen");
    var problem = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()
        .CreateProblemDetails(context.HttpContext, statusCode: StatusCodes.Status400BadRequest,
            title: "Ugyldig forespørsel", detail: error.Message, instance: context.HttpContext.Request.Path);
    problem.Extensions["ftbidFeil"] = FtbidError.FromException(error);
    problem.Extensions["sporingsidentifikator"] = context.HttpContext.TraceIdentifier;
    var result = new BadRequestObjectResult(problem);
    result.ContentTypes.Add("application/problem+json");
    return result;
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
    JsonResponseConfiguration.IncludeAllValues(options.SerializerOptions);
});
builder.Services.AddExceptionHandler<FtbidExceptionHandler>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
    context.ProblemDetails.Extensions["sporingsidentifikator"] = context.HttpContext.TraceIdentifier;
    context.ProblemDetails.Title = context.ProblemDetails.Status switch
    {
        400 => "Ugyldig forespørsel", 401 => "Autentisering kreves", 403 => "Tilgang nektet",
        404 => context.ProblemDetails.Detail is null ? "Endepunktet finnes ikke" : context.ProblemDetails.Title, 405 => "HTTP-metoden er ikke tillatt", 429 => "For mange forespørsler",
        >= 500 => "Intern serverfeil", _ => context.ProblemDetails.Title
    };
});
builder.Services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("oppretting", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("organisasjonsnummer")?.Value ?? "ukjent", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 200, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SchemaFilter<TilknytningSchemaFilter>();
    options.OperationFilter<FtbidSokOperationFilter>();
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Ftbid-servicen", Version = "v1", Description = "Prosjektidentifikatorer og identifikasjonsmetadata." });
    if (maskinportenEnabled)
    {
        options.AddSecurityDefinition("Maskinporten", new OpenApiSecurityScheme
        { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", Description = builder.Environment.IsEnvironment("Local") || builder.Environment.IsDevelopment()
            ? "Local og Development krever kun et Bearer-token. Tokenets innhold og scopes valideres ikke."
            : "Tilgangstoken fra Maskinporten med les- eller endrescope." });
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        { [new OpenApiSecuritySchemeReference("Maskinporten", document)] = [] });
    }
});
var app = builder.Build();
if (serilogEnabled)
    app.UseSerilogRequestLogging();
var importDevdata = builder.Configuration.GetValue<bool>("Database:ImporterDevdata");
if (importDevdata && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Database:ImporterDevdata er bare tillatt i Development.");
if (args.Contains("--migrer", StringComparer.Ordinal) || args.Contains("--importer", StringComparer.Ordinal))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<FtbidDbContext>();
    await database.Database.MigrateAsync();
    var index = Array.IndexOf(args, "--importer");
    if (index >= 0)
    {
        if (index + 1 >= args.Length) throw new RequiredValueMissingException("Filsti etter --importer");
        await scope.ServiceProvider.GetRequiredService<IReferansedataImportService>()
            .ImportAsync(args[index + 1]);
    }
    return;
}
if (builder.Configuration.GetValue<bool>("Database:MigrerVedOppstart") || importDevdata)
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<FtbidDbContext>().Database.MigrateAsync();
}
if (importDevdata)
{
    using var scope = app.Services.CreateScope();
    var imported = await DevdataImport.ImportAsync(
        scope.ServiceProvider.GetRequiredService<FtbidDbContext>(),
        Path.Combine(AppContext.BaseDirectory, "devdata", "lokal.sql.gz"));
    app.Logger.LogInformation("Devdata: {Resultat}", imported ? "Importert fra lokalt snapshot" : "Allerede importert");
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
    ResponseWriter = async (context, rapport) => await context.Response.WriteAsJsonAsync(new
    { Status = rapport.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy ? "Frisk" : "Usunn", Beskrivelse = rapport.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy ? "Servicen og PostgreSQL er tilgjengelige." : "Databasen er ikke klar." })
}).AllowAnonymous();
app.MapGet("/helse/levende", () => Results.Ok(new { Status = "Tilgjengelig" })).AllowAnonymous().ExcludeFromDescription();
if (builder.Configuration.GetValue<bool>("Swagger:Aktivert"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options => { options.SwaggerEndpoint("/openapi/v1.json", "Ftbid-servicen v1"); options.DocumentTitle = "Ftbid-servicen"; });
}
app.Run();

/// <summary>API-ets inngangspunkt, tilgjengelig for integrasjonstester.</summary>
public partial class Program;
