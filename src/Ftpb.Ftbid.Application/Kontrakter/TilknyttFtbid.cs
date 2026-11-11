namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Tilknytning av referanser til skjema, rapport eller andre ressurser.</summary>
public sealed class TilknyttFtbid
{
    public string? Ftbid { get; set; }
    public List<TilknytningForesporsel>? Tilknytninger { get; set; }
}
