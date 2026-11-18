namespace Ftpb.Ftbid.Application.Contracts;

/// <summary>Referanser som skal knyttes til ftbid angitt i URL-en.</summary>
public sealed class TilknyttFtbidRequest
{
    public List<TilknytningRequest>? Tilknytninger { get; set; }
}
