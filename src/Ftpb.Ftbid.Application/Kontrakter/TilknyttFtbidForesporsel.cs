namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Referanser som skal knyttes til ftbid angitt i URL-en.</summary>
public sealed class TilknyttFtbidForesporsel
{
    public List<TilknytningForesporsel>? Tilknytninger { get; set; }
}
