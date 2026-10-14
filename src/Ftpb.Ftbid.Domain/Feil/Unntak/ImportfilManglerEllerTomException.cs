using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>En referansedatafil mangler eller inneholder ingen data.</summary>
public sealed class ImportfilManglerEllerTomException(Exception? indreUnntak = null)
    : ValideringsException($"Importfilen mangler eller er tom.", Feilkode.V003, indreUnntak)
{
}
