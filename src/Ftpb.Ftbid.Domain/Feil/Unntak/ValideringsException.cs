using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Grunnklasse for forventede inndata- og forretningsfeil som gir HTTP 400.</summary>
public abstract class ValideringsException(string melding, Feilkode kode, Exception? indreUnntak = null)
    : FtbidException(melding, kode, FeilType.Feil, indreUnntak);
