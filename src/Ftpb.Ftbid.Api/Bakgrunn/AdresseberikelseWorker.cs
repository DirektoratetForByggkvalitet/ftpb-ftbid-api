using Ftpb.Ftbid.Infrastructure.Matrikkel;
using Microsoft.Extensions.Options;

namespace Ftpb.Ftbid.Api.Bakgrunn;

/// <summary>Etterfyller eksisterende eiendommer når aktivert. Deaktivert som standard.</summary>
public sealed class AdresseberikelseWorker(IServiceScopeFactory omfangsfabrikk,
    IOptionsMonitor<AdresseberikelseInnstillinger> innstillinger, IOptionsMonitor<MatrikkelInnstillinger> matrikkel,
    ILogger<AdresseberikelseWorker> logger) : BackgroundService
{
    /// <summary>Behandler avgrensede batcher og prøver feilede eller nye rader i neste gjennomgang.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        long etterId = 0;
        var nesteGjennomgang = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var valg = innstillinger.CurrentValue;
                if (!valg.Aktivert)
                {
                    etterId = 0;
                    nesteGjennomgang = DateTimeOffset.MinValue;
                }
                else if (DateTimeOffset.UtcNow >= nesteGjennomgang)
                {
                    matrikkel.CurrentValue.Valider();
                    if (valg.Batchstorrelse is < 1 or > 1000 || valg.PauseMellomOppslagMs is < 0 or > 60000 || valg.IntervallMinutter < 1)
                        throw new InvalidOperationException("Adresseberikelse krever Batchstorrelse 1–1000, PauseMellomOppslagMs 0–60000 og positivt IntervallMinutter.");
                    await using var omfang = omfangsfabrikk.CreateAsyncScope();
                    var sisteId = await omfang.ServiceProvider.GetRequiredService<Adresseberikelse>()
                        .BehandleBatchAsync(etterId, valg.Batchstorrelse, valg.PauseMellomOppslagMs,
                            () => innstillinger.CurrentValue.Aktivert, stoppingToken);
                    if (sisteId.HasValue)
                    {
                        etterId = sisteId.Value;
                        continue;
                    }
                    logger.LogInformation("Adresseberikelse: gjennomgang ferdig. Ny kontroll om {Minutter} minutter", valg.IntervallMinutter);
                    etterId = 0;
                    nesteGjennomgang = DateTimeOffset.UtcNow.AddMinutes(valg.IntervallMinutter);
                }
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception feil)
            {
                logger.LogError(feil, "Adresseberikelse kunne ikke kjøre; nytt forsøk om 30 sekunder");
                try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
