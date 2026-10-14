using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Kartverkets svar kunne ikke tolkes som et gyldig oppslag.</summary>
public sealed class UgyldigMatrikkelsvarException(Exception? indreUnntak = null)
    : FtbidException("Matrikkelen returnerte et ugyldig svar. Matrikkelopplysningene er ikke knyttet til prosjektet.", Feilkode.V0065, FeilType.Feil, indreUnntak);
