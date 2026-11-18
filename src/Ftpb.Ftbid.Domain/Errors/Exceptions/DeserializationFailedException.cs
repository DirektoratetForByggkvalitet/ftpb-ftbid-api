using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>Inndata kan ikke deserialiseres til forventet modell.</summary>
public sealed class DeserializationFailedException(string targetType, Exception? innerException = null)
    : ValidationException($"Forespørselen kunne ikke leses som {targetType}. Kontroller felter og datatyper.", Feilkode.V0038, innerException)
{
    public string Måltype { get; } = targetType;
}
