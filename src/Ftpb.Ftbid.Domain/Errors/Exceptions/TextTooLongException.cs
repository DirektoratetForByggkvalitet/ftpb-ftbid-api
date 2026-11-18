using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>En tekstverdi overskrider tillatt lengde.</summary>
public sealed class TextTooLongException(string valueName, int maxLength, Exception? innerException = null)
    : ValidationException($"{valueName} kan ha maksimalt {maxLength} tegn.", Feilkode.V0047, innerException)
{
    public string VerdiNavn { get; } = valueName;
    public int MaksLengde { get; } = maxLength;
}
