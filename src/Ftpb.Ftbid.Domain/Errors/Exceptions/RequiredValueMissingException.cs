using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>En påkrevd verdi mangler.</summary>
public sealed class RequiredValueMissingException(string valueName, Exception? innerException = null)
    : ValidationException($"Påkrevd verdi mangler: {valueName}.", Feilkode.V0042, innerException)
{
    public string VerdiNavn { get; } = valueName;
}
