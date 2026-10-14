using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Kartverket avviste servicens Basic-autentisering.</summary>
public sealed class MatrikkelAutentiseringFeiletException(Exception? indreUnntak = null)
    : FtbidException("Servicen fikk ikke tilgang til Matrikkelen. Matrikkelopplysningene er ikke knyttet til prosjektet.", Feilkode.V0066, FeilType.Feil, indreUnntak);
