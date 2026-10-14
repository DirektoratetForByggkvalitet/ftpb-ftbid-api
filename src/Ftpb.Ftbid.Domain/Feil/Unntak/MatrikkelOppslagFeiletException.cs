using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Kartverket svarte med servicefeil eller oppslaget kunne ikke gjennomføres.</summary>
public sealed class MatrikkelOppslagFeiletException(Exception? indreUnntak = null)
    : FtbidException("Matrikkelen er ikke tilgjengelig. Matrikkelopplysningene er ikke knyttet til prosjektet.", Feilkode.V0064, FeilType.Feil, indreUnntak);
