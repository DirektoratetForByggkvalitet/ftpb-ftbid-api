using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ftpb.Ftbid.Domain.Prosjekter;

/// <summary>Entydig sammenligning av tiltakstyper uavhengig av rekkefølge og duplikater.</summary>
public static class TiltakstypeSett
{
    public static string NormalizeText(string? value) => string.Join(' ',
        (value ?? "").Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    public static string[] Normalize(IEnumerable<string> values) => values.Select(NormalizeText)
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    public static string ComputeHash(IEnumerable<string> values) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(Normalize(values))));
}
