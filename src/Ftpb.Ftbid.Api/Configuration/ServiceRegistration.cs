using Ftpb.Ftbid.Api.Authentication;
using Ftpb.Ftbid.Api.BackgroundServices;
using Ftpb.Ftbid.Application.Matrikkel;
using Ftpb.Ftbid.Infrastructure.Matrikkel;
using Ftpb.Ftbid.Application.Configuration;
using Ftpb.Ftbid.Application.Repositories;
using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Domain.Errors.Exceptions;
using Ftpb.Ftbid.Domain.Identifikatorer;
using Ftpb.Ftbid.Infrastructure.Persistence;
using Ftpb.Ftbid.Infrastructure.Persistence.Repositories;
using Ftpb.Ftbid.Infrastructure.Referansedata;
using Microsoft.EntityFrameworkCore;

namespace Ftpb.Ftbid.Api.Configuration;

/// <summary>Setter sammen applikasjonens servicer og PostgreSQL-avhengigheter.</summary>
public static class ServiceRegistration
{
    /// <summary>Validerer konfigurasjonen og registrerer autentisering, lagring og forretningsservicer.</summary>
    public static IServiceCollection AddFtbidServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString)) throw new MissingConfigurationException("ConnectionStrings:Postgres");
        var ftbidOptions = configuration.GetSection("Ftbid").Get<FtbidOptions>() ?? new();
        _ = EanGenerator.Generate(ftbidOptions.Gs1Prefiks, 1);
        var maskinporten = configuration.GetSection("Maskinporten").Get<MaskinportenOptions>() ?? new();
        services.AddMaskinporten(maskinporten, environment);
        services.AddSingleton(ftbidOptions);
        var useEntraId = configuration.GetValue<bool>("Database:BrukEntraId");
        services.AddDbContext<FtbidDbContext>(options => PostgresConfiguration.Configure(options, connectionString, useEntraId));
        services.AddSingleton<IFtbidDublettService, FtbidDublettService>();
        services.AddScoped<IFtbidRepository, PostgresFtbidRepository>();
        services.AddScoped<IFtbidEiendomRepository, PostgresFtbidEiendomRepository>();
        services.AddScoped<IFtbidEiendomService, FtbidEiendomService>();
        services.AddScoped<IKommuneregister, PostgresKommuneregister>();
        services.AddScoped<IFtbidOpprettingService, FtbidOpprettingService>();
        services.AddScoped<IFtbidOppslagService, FtbidOppslagService>();
        services.AddScoped<IFtbidTilknytningService, FtbidTilknytningService>();
        services.AddScoped<IReferansedataImportService, ReferansedataImportService>();
        services.Configure<MatrikkelOptions>(configuration.GetSection("Matrikkel"));
        services.Configure<AdresseberikelseOptions>(configuration.GetSection("Adresseberikelse"));
        services.AddScoped<IMatrikkelAdresseoppslag, MatrikkelAdresseoppslag>();
        services.AddScoped<Adresseberikelse>();
        services.AddHostedService<AdresseberikelseWorker>();
        return services;
    }
}
