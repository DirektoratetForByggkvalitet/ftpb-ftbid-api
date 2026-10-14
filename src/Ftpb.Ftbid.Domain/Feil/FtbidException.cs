namespace Ftpb.Ftbid.Domain.Feil;

/// <summary>Felles grunnklasse for forventede ftbid-feil med regelkode og feiltype.</summary>
public abstract class FtbidException(string melding, Feilkode kode, FeilType feilType, Exception? indreUnntak = null)
    : Exception(melding, indreUnntak)
{
    public Feilkode Kode { get; } = kode;
    public FeilType FeilType { get; } = feilType;

    /// <summary>Beskriver feiltypen, regelkoden og den norske feilmeldingen.</summary>
    public override string ToString() => $"{FeilType} {Kode}: {Message}";
}
