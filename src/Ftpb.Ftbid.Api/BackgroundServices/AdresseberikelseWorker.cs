using Ftpb.Ftbid.Infrastructure.Matrikkel;
using Microsoft.Extensions.Options;

namespace Ftpb.Ftbid.Api.BackgroundServices;

/// <summary>Etterfyller eksisterende eiendommer når aktivert. Deaktivert som standard.</summary>
public sealed class AdresseberikelseWorker(IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AdresseberikelseOptions> settings, IOptionsMonitor<MatrikkelOptions> matrikkel,
    ILogger<AdresseberikelseWorker> logger) : BackgroundService
{
    /// <summary>Behandler avgrensede batcher og prøver feilede eller nye rader i neste gjennomgang.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        long afterId = 0;
        var nesteGjennomgang = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var options = settings.CurrentValue;
                if (!options.Enabled)
                {
                    afterId = 0;
                    nesteGjennomgang = DateTimeOffset.MinValue;
                }
                else if (DateTimeOffset.UtcNow >= nesteGjennomgang)
                {
                    matrikkel.CurrentValue.Validate();
                    if (options.BatchSize is < 1 or > 1000 || options.LookupDelayMs is < 0 or > 60000 || options.IntervalMinutes < 1)
                        throw new InvalidOperationException("Adresseberikelse krever Batchstorrelse 1–1000, PauseMellomOppslagMs 0–60000 og positivt IntervallMinutter.");
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var lastId = await scope.ServiceProvider.GetRequiredService<Adresseberikelse>()
                        .ProcessBatchAsync(afterId, options.BatchSize, options.LookupDelayMs,
                            () => settings.CurrentValue.Enabled, stoppingToken);
                    if (lastId.HasValue)
                    {
                        afterId = lastId.Value;
                        continue;
                    }
                    logger.LogInformation("Adresseberikelse: gjennomgang ferdig. Ny kontroll om {Minutter} minutter", options.IntervalMinutes);
                    afterId = 0;
                    nesteGjennomgang = DateTimeOffset.UtcNow.AddMinutes(options.IntervalMinutes);
                }
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                logger.LogError(error, "Adresseberikelse kunne ikke kjøre; nytt forsøk om 30 sekunder");
                try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
