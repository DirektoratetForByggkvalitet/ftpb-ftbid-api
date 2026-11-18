using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>En nødvendig konfigurasjonsverdi mangler ved oppstart.</summary>
public sealed class MissingConfigurationException(string valueName)
    : FtbidException($"Konfigurasjonsverdien {valueName} må fylles ut.", Feilkode.V0059, FeilType.Feil)
{
    public string VerdiNavn { get; } = valueName;
}
