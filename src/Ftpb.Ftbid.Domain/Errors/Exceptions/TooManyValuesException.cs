using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>En liste inneholder flere verdier enn tillatt.</summary>
public sealed class TooManyValuesException(string valueName, int maxCount, Exception? innerException = null)
    : ValidationException($"{valueName} kan ha maksimalt {maxCount} verdier.", Feilkode.V0048, innerException)
{
    public string VerdiNavn { get; } = valueName;
    public int MaksAntall { get; } = maxCount;
}
