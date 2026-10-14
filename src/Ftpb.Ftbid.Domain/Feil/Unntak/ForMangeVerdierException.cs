using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>En liste inneholder flere verdier enn tillatt.</summary>
public sealed class ForMangeVerdierException(string verdiNavn, int maksAntall, Exception? indreUnntak = null)
    : ValideringsException($"{verdiNavn} kan ha maksimalt {maksAntall} verdier.", Feilkode.V0048, indreUnntak)
{
    public string VerdiNavn { get; } = verdiNavn;
    public int MaksAntall { get; } = maksAntall;
}
