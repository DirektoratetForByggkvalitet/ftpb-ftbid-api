using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>En matrikkelenhet mangler eller inneholder ugyldige nummer.</summary>
public sealed class UgyldigeMatrikkelopplysningerException(Exception? indreUnntak = null)
    : ValideringsException($"Matrikkelopplysninger må fylles ut og ha ikke-negative nummer.", Feilkode.V0049, indreUnntak)
{
}
