using Ftpb.Ftbid.Application.Configuration;
using Ftpb.Ftbid.Domain.Errors.Exceptions;

namespace Ftpb.Ftbid.Application.Validation;

/// <summary>Felles normalisering og tekstvalidering for prosjektidentifikatorer.</summary>
internal static class FtbidValidation
{
    internal static string NormalizeFtbid(string? ftbid, FtbidOptions settings)
    {
        var value = RequireText(ftbid, "Ftbid", 18);
        if (value.Length is not (8 or 18) || !value.All(char.IsAsciiDigit))
            throw new UgyldigFtbidException();
        return value.Length == 8 ? $"{settings.Gs1Prefiks}000{value}" : value;
    }

    internal static string? NormalizeAnsvarligSoker(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var nummer = value.Trim();
        if (nummer.Length != 9 || !nummer.All(char.IsAsciiDigit))
            throw new UgyldigOrganisasjonsnummerException();
        return nummer;
    }

    internal static string RequireText(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new RequiredValueMissingException(name);
        if (value.Trim().Length > maxLength)
            throw new TextTooLongException(name, maxLength);
        return value.Trim();
    }
}
