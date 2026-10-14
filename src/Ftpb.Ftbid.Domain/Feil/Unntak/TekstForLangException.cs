using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>En tekstverdi overskrider tillatt lengde.</summary>
public sealed class TekstForLangException(string verdiNavn, int maksLengde, Exception? indreUnntak = null)
    : ValideringsException($"{verdiNavn} kan ha maksimalt {maksLengde} tegn.", Feilkode.V0047, indreUnntak)
{
    public string VerdiNavn { get; } = verdiNavn;
    public int MaksLengde { get; } = maksLengde;
}
