namespace Ftpb.Ftbid.Application.Contracts;

/// <summary>Referanse og metadata som skal registreres og knyttes til en ftbid.</summary>
public sealed class TilknytningRequest
{
    public string? ReferanseId { get; set; }
    public string? Typebeskrivelse { get; set; }
    public List<string> Tiltakstyper { get; set; } = [];
}
