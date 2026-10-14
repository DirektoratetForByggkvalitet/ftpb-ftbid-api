using Ftpb.Ftbid.Application.Konfigurasjon;
using Ftpb.Ftbid.Domain.Feil.Unntak;

namespace Ftpb.Ftbid.Application.Validering;

/// <summary>Felles normalisering og tekstvalidering for prosjektidentifikatorer.</summary>
internal static class FtbidValidering
{
    internal static string NormaliserFtbid(string? ftbid, FtbidInnstillinger innstillinger)
    {
        var verdi = KrevTekst(ftbid, "Ftbid", 18);
        if (verdi.Length is not (8 or 18) || !verdi.All(char.IsAsciiDigit))
            throw new UgyldigFtbidException();
        return verdi.Length == 8 ? $"{innstillinger.Gs1Prefiks}000{verdi}" : verdi;
    }

    internal static string KrevTekst(string? verdi, string navn, int maks)
    {
        if (string.IsNullOrWhiteSpace(verdi))
            throw new PåkrevdVerdiManglerException(navn);
        if (verdi.Trim().Length > maks)
            throw new TekstForLangException(navn, maks);
        return verdi.Trim();
    }
}
