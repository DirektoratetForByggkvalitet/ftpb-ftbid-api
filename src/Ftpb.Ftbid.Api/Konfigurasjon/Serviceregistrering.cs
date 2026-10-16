using Ftpb.Ftbid.Api.Autentisering;
using Ftpb.Ftbid.Application.Konfigurasjon;
using Ftpb.Ftbid.Application.Lagring;
using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using Ftpb.Ftbid.Domain.Identifikatorer;
using Ftpb.Ftbid.Infrastructure.Persistens;
using Ftpb.Ftbid.Infrastructure.Persistens.Lagre;
using Ftpb.Ftbid.Infrastructure.Referansedata;
using Microsoft.EntityFrameworkCore;

namespace Ftpb.Ftbid.Api.Konfigurasjon;

/// <summary>Setter sammen applikasjonens servicer og PostgreSQL-avhengigheter.</summary>
public static class Serviceregistrering
{
    /// <summary>Validerer konfigurasjonen og registrerer autentisering, lagring og forretningsservicer.</summary>
    public static IServiceCollection LeggTilFtbidServices(this IServiceCollection services, IConfiguration konfigurasjon, IHostEnvironment miljo)
    {
        var tilkobling = konfigurasjon.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(tilkobling)) throw new KonfigurasjonManglerException("ConnectionStrings:Postgres");
        var ftbidInnstillinger = konfigurasjon.GetSection("Ftbid").Get<FtbidInnstillinger>() ?? new();
        _ = EanGenerator.Lag(ftbidInnstillinger.Gs1Prefiks, 1);
        var maskinporten = konfigurasjon.GetSection("Maskinporten").Get<MaskinportenInnstillinger>() ?? new();
        services.LeggTilMaskinporten(maskinporten, miljo);
        services.AddSingleton(ftbidInnstillinger);
        var brukEntraId = konfigurasjon.GetValue<bool>("Database:BrukEntraId");
        services.AddDbContext<FtbidDbContext>(valg => PostgresOppsett.Konfigurer(valg, tilkobling, brukEntraId));
        services.AddSingleton<IFtbidDublettService, FtbidDublettService>();
        services.AddScoped<IFtbidLager, PostgresFtbidLager>();
        services.AddScoped<IFtbidEiendomslager, PostgresFtbidEiendomslager>();
        services.AddScoped<IFtbidEiendomService, FtbidEiendomService>();
        services.AddScoped<IKommuneregister, PostgresKommuneregister>();
        services.AddScoped<IFtbidOpprettingService, FtbidOpprettingService>();
        services.AddScoped<IFtbidOppslagService, FtbidOppslagService>();
        services.AddScoped<IFtbidTilknytningService, FtbidTilknytningService>();
        services.AddScoped<IReferansedataImportService, ReferansedataImportService>();
        return services;
    }
}
