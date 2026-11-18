namespace Ftpb.Ftbid.Domain.Errors;

/// <summary>Felles grunnklasse for forventede ftbid-feil med regelkode og feiltype.</summary>
public abstract class FtbidException(string message, Feilkode kode, FeilType feilType, Exception? innerException = null)
    : Exception(message, innerException)
{
    public Feilkode Kode { get; } = kode;
    public FeilType FeilType { get; } = feilType;

    /// <summary>Beskriver feiltypen, regelkoden og den norske feilmeldingen.</summary>
    public override string ToString() => $"{FeilType} {Kode}: {Message}";
}
