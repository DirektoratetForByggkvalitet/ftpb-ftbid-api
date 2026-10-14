namespace Ftpb.Ftbid.Domain.Prosjekter;

/// <summary>En adresse knyttet til prosjektet. Rekkefølgen bevarer hovedadressen.</summary>
public sealed class Adresse
{
    public long Id { get; set; }
    public string Ftbid { get; set; } = "";
    public int Rekkefolge { get; set; }
    public string Adressetekst { get; set; } = "";
}
