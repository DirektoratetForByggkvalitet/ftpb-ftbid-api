using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>En påkrevd verdi mangler.</summary>
public sealed class PåkrevdVerdiManglerException(string verdiNavn, Exception? indreUnntak = null)
    : ValideringsException($"Påkrevd verdi mangler: {verdiNavn}.", Feilkode.V0042, indreUnntak)
{
    public string VerdiNavn { get; } = verdiNavn;
}
