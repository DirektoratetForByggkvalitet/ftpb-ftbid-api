namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Tilknytning av en eksisterende referanse til skjema, rapport eller andre ressurser.</summary>
public sealed class TilknyttFtbid
{
    public string? Ftbid { get; set; }
    public string? ReferanseId { get; set; }
}
