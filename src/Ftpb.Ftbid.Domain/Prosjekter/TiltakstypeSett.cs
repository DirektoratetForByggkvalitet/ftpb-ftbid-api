using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ftpb.Ftbid.Domain.Prosjekter;

/// <summary>Entydig sammenligning av tiltakstyper uavhengig av rekkefølge og duplikater.</summary>
public static class TiltakstypeSett
{
    public static string NormaliserTekst(string? verdi) => string.Join(' ',
        (verdi ?? "").Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    public static string[] Normaliser(IEnumerable<string> verdier) => verdier.Select(NormaliserTekst)
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    public static string LagHash(IEnumerable<string> verdier) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(Normaliser(verdier))));
}
