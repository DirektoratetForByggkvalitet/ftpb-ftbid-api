using System.Globalization;
using Ftpb.Ftbid.Domain.Errors.Exceptions;

namespace Ftpb.Ftbid.Domain.Identifikatorer;

/// <summary>Genererer samme 18-sifrede EAN-format som den eksisterende adapteren.</summary>
public static class EanGenerator
{
    /// <summary>Bygger GS1-prefiks, tre reserverte nuller, sekvensnummer og kontrollsiffer.</summary>
    public static string Generate(string gs1Prefiks, long sekvensnummer)
    {
        if (gs1Prefiks.Length != 7 || !gs1Prefiks.All(char.IsAsciiDigit))
            throw new UgyldigGs1PrefiksException();
        if (sekvensnummer is < 1 or > 9_999_999)
            throw new SekvensnummerUtenforOmrådetException(sekvensnummer);
        var connectionStringBuilder = gs1Prefiks + "000" + sekvensnummer.ToString("D7", CultureInfo.InvariantCulture);
        var sum = connectionStringBuilder.Select((siffer, i) => (siffer - '0') * (i % 2 == 0 ? 3 : 1)).Sum();
        return connectionStringBuilder + ((10 - sum % 10) % 10).ToString(CultureInfo.InvariantCulture);
    }
}
