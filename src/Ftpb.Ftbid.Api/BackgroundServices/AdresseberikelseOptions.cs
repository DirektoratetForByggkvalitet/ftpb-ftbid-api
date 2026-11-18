using Microsoft.Extensions.Configuration;

namespace Ftpb.Ftbid.Api.BackgroundServices;

/// <summary>Styrer bakgrunnsjobben for utfylling av matrikkeladresser.</summary>
public sealed class AdresseberikelseOptions
{
    [ConfigurationKeyName("Aktivert")]
    public bool Enabled { get; set; }
    [ConfigurationKeyName("Batchstorrelse")]
    public int BatchSize { get; set; } = 50;
    [ConfigurationKeyName("PauseMellomOppslagMs")]
    public int LookupDelayMs { get; set; } = 250;
    [ConfigurationKeyName("IntervallMinutter")]
    public int IntervalMinutes { get; set; } = 60;
}
