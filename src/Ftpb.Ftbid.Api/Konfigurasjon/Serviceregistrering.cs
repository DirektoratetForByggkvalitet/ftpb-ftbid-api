using Ftpb.Ftbid.Application.Matrikkel;
using Ftpb.Ftbid.Infrastructure.Integrasjoner.Matrikkel;
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
        var matrikkel = konfigurasjon.GetSection("Matrikkel").Get<MatrikkelInnstillinger>() ?? new();
        services.AddSingleton(matrikkel);
        services.AddHttpClient<IMatrikkelKlient, KartverketMatrikkelKlient>(klient =>
        {
            if (matrikkel.TidsgrenseSekunder is < 1 or > 120)
                throw new KonfigurasjonManglerException("Matrikkel:TidsgrenseSekunder må være mellom 1 og 120");
            klient.Timeout = TimeSpan.FromSeconds(matrikkel.TidsgrenseSekunder);
            klient.MaxResponseContentBufferSize = 30_720_000;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IMatrikkelService, MatrikkelService>();
        services.AddSingleton(ftbidInnstillinger);
        services.AddDbContext<FtbidDbContext>(valg => valg.UseNpgsql(tilkobling));
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
