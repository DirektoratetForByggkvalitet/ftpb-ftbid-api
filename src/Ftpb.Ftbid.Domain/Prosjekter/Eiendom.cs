namespace Ftpb.Ftbid.Domain.Prosjekter;

/// <summary>En eiendom med identifikasjon og adresser, knyttet til en ftbid.</summary>
public sealed class Eiendom
{
    public long Id { get; set; }
    public string Ftbid { get; set; } = "";
    public int Rekkefolge { get; set; }
    public Eiendomsidentifikasjon Eiendomsidentifikasjon { get; set; } = new("", 0, 0);
    public List<Eiendomsadresse> Adresse { get; set; } = [];
    public string? Bygningsnummer { get; set; }
    public string? Bolignummer { get; set; }
    public string? Kommunenavn { get; set; }
}
