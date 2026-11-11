namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Referanse og metadata som skal registreres og knyttes til en ftbid.</summary>
public sealed class TilknytningForesporsel
{
    public string? ReferanseId { get; set; }
    public string? Typebeskrivelse { get; set; }
    public List<string> Tiltakstyper { get; set; } = [];
}
