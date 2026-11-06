using System.Text.RegularExpressions;
using Ftpb.Ftbid.Domain.Feil.Unntak;

namespace Ftpb.Ftbid.Application.Validering;

/// <summary>Kontrollerer generell URN-syntaks etter RFC 8141, uten oppslag i navneromsregistre.</summary>
public static class UrnValidering
{
    private const string Pchar = "(?:[A-Za-z0-9._~!$&'()*+,;=:@-]|%[0-9A-Fa-f]{2})";
    /// <summary>Felles mønster for validering og OpenAPI. Navnerommet har 2–32 tegn.</summary>
    public const string Monster = "^[uU][rR][nN]:[A-Za-z0-9][A-Za-z0-9-]{0,30}[A-Za-z0-9]:"
        + Pchar + "(?:" + Pchar + "|/)*"
        + "(?:\\?\\+" + Pchar + "(?:" + Pchar + "|/|\\?(?!=))*)?"
        + "(?:\\?=" + Pchar + "(?:" + Pchar + "|/|\\?)*)?"
        + "(?:#(?:" + Pchar + "|/|\\?)*)?$(?![\\s\\S])";

    private static readonly Regex Urn = new(Monster, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Avviser ugyldige URN-er og bevarer identifikatorens tegn og bokstavstørrelse.</summary>
    public static string KrevUrn(string verdi) => Urn.IsMatch(verdi) ? verdi : throw new UgyldigUrnException();
}
