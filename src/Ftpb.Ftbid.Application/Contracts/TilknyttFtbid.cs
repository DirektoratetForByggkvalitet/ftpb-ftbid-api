namespace Ftpb.Ftbid.Application.Contracts;

/// <summary>Tilknytning av referanser til skjema, rapport eller andre ressurser.</summary>
public sealed class TilknyttFtbid
{
    public string? Ftbid { get; set; }
    public List<TilknytningRequest>? Tilknytninger { get; set; }
}
