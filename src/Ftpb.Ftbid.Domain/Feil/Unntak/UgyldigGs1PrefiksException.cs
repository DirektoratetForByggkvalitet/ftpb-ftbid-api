using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>GS1-prefikset i servicens konfigurasjon er ugyldig.</summary>
public sealed class UgyldigGs1PrefiksException(Exception? indreUnntak = null)
    : FtbidException($"GS1-prefikset må bestå av sju sifre.", Feilkode.V0057, FeilType.Feil, indreUnntak)
{
}
