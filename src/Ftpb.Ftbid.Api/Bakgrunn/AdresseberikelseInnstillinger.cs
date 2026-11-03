namespace Ftpb.Ftbid.Api.Bakgrunn;

/// <summary>Styrer bakgrunnsjobben for utfylling av matrikkeladresser.</summary>
public sealed class AdresseberikelseInnstillinger
{
    public bool Aktivert { get; set; }
    public int Batchstorrelse { get; set; } = 50;
    public int PauseMellomOppslagMs { get; set; } = 250;
    public int IntervallMinutter { get; set; } = 60;
}
