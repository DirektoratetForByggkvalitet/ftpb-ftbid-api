using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Ingen søkeparameter er oppgitt.</summary>
public sealed class SøkeparameterManglerException(Exception? indreUnntak = null)
    : ValideringsException($"Oppgi minst én søkeparameter.", Feilkode.V0050, indreUnntak)
{
}
