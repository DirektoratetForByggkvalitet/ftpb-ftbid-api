using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>Grunnklasse for forventede inndata- og forretningsfeil som gir HTTP 400.</summary>
public abstract class ValidationException(string message, Feilkode kode, Exception? innerException = null)
    : FtbidException(message, kode, FeilType.Feil, innerException);
