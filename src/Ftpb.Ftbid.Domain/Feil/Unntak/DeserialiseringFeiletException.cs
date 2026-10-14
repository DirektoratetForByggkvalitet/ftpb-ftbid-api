using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Inndata kan ikke deserialiseres til forventet modell.</summary>
public sealed class DeserialiseringFeiletException(string måltype, Exception? indreUnntak = null)
    : ValideringsException($"Forespørselen kunne ikke leses som {måltype}. Kontroller felter og datatyper.", Feilkode.V0038, indreUnntak)
{
    public string Måltype { get; } = måltype;
}
