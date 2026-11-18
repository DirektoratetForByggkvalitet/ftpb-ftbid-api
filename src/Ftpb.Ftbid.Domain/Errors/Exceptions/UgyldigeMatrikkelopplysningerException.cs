using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>En matrikkelenhet mangler eller inneholder ugyldige nummer.</summary>
public sealed class UgyldigeMatrikkelopplysningerException(Exception? innerException = null)
    : ValidationException($"Matrikkelopplysninger må fylles ut og ha ikke-negative nummer.", Feilkode.V0049, innerException)
{
}
