using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>En referansedatafil mangler eller inneholder ingen data.</summary>
public sealed class ImportFileMissingOrEmptyException(Exception? innerException = null)
    : ValidationException($"Importfilen mangler eller er tom.", Feilkode.V003, innerException)
{
}
