using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>Ingen søkeparameter er oppgitt.</summary>
public sealed class SøkeparameterManglerException(Exception? innerException = null)
    : ValidationException($"Oppgi minst én søkeparameter.", Feilkode.V0050, innerException)
{
}
