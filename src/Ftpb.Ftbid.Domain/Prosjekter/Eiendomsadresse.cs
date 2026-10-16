namespace Ftpb.Ftbid.Domain.Prosjekter;

/// <summary>Strukturert adresse på én eiendom.</summary>
public sealed class Eiendomsadresse
{
    public long Id { get; set; }
    public long EiendomId { get; set; }
    public int Rekkefolge { get; set; }
    public string? Adresselinje1 { get; set; }
    public string? Adresselinje2 { get; set; }
    public string? Adresselinje3 { get; set; }
    public string? Postnr { get; set; }
    public string? Poststed { get; set; }
    public string? Landkode { get; set; }
    public string? Gatenavn { get; set; }
    public string? Husnr { get; set; }
    public string? Bokstav { get; set; }
}
