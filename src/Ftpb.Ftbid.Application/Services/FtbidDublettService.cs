using System.Text;
using FtbidEntitet = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Matcher kommuner, fullstendige matrikkelnumre og normaliserte prosjektopplysninger.</summary>
public sealed class FtbidDublettService : IFtbidDublettService
{
    /// <inheritdoc />
    public FtbidDublettTreff? Finn(FtbidEntitet prosjekt, IEnumerable<FtbidEntitet> kandidater) =>
        kandidater.Select(k => Vurder(prosjekt, k)).OfType<FtbidDublettTreff>()
            .OrderBy(t => t.Sannsynlig).ThenByDescending(t => t.SamsvarendeFelter.Count)
            .ThenBy(t => t.Ftbid, StringComparer.Ordinal).FirstOrDefault();

    private static FtbidDublettTreff? Vurder(FtbidEntitet ny, FtbidEntitet gammel)
    {
        var kommuner = ny.Matrikkelopplysninger.Select(m => m.Kommunenummer).Append(ny.Kommunenummer).ToHashSet();
        if (!gammel.Matrikkelopplysninger.Select(m => m.Kommunenummer).Append(gammel.Kommunenummer).Any(kommuner.Contains))
            return null;
        var felter = new List<string> { "Kommune" };
        bool Match(string felt, string? a, string? b)
        {
            if (!Lik(a, b)) return false;
            felter.Add(felt);
            return true;
        }
        var matrikkel = ny.Matrikkelopplysninger.Any(a => gammel.Matrikkelopplysninger.Any(b =>
            a.Kommunenummer == b.Kommunenummer && a.Gaardsnummer == b.Gaardsnummer &&
            a.Bruksnummer == b.Bruksnummer && a.Festenummer == b.Festenummer && a.Seksjonsnummer == b.Seksjonsnummer));
        if (matrikkel) felter.Add("Matrikkelopplysninger");
        var adresse = ny.Adresser.Any(a => gammel.Adresser.Any(b => Lik(a.Adressetekst, b.Adressetekst)));
        if (adresse) felter.Add("Adresse");
        var navn = Match("Prosjektnavn", ny.Prosjektnavn, gammel.Prosjektnavn);
        var referanse = ny.Kommunenummer == gammel.Kommunenummer && Lik(ny.System, gammel.System) &&
            Match("EksternProsjektreferanse", ny.EksternProsjektreferanse ?? ny.Prosjektnummer,
                gammel.EksternProsjektreferanse ?? gammel.Prosjektnummer);
        // Saksnummer og eksterne referanser gjelder hovedkommunen; sekundærkommuner kan ikke gi et slikt treff.
        var sak = ny.Kommunenummer == gammel.Kommunenummer &&
            Match("KommunaltSaksnummer", ny.KommunaltSaksnummer, gammel.KommunaltSaksnummer);
        var organisasjon = Match("Organisasjonsnummer", ny.Organisasjonsnummer, gammel.Organisasjonsnummer);
        var tiltakshaver = Match("Tiltakshaver", ny.Tiltakshaver, gammel.Tiltakshaver);
        var bygning = Match("Bygningsnummer", ny.Bygningsnummer, gammel.Bygningsnummer);
        var tiltak = Match("Tiltakstype", ny.Tiltakstype, gammel.Tiltakstype);
        if (sak || referanse || (matrikkel && navn)) return new(gammel.Ean, false, felter);
        var lignendeNavn = !navn && Lignende(ny.Prosjektnavn, gammel.Prosjektnavn);
        if (lignendeNavn) felter.Add("Prosjektnavn (lignende)");
        var stedsmatch = matrikkel || adresse || bygning;
        var stottesignaler = ((organisasjon || tiltakshaver) ? 1 : 0) + (bygning ? 1 : 0) + (tiltak ? 1 : 0);
        return stedsmatch && (navn || lignendeNavn || stottesignaler >= 2)
            ? new(gammel.Ean, true, felter) : null;
    }

    private static string Normaliser(string? verdi) => string.Join(' ',
        (verdi ?? "").Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    private static bool Lik(string? a, string? b) => Normaliser(a) is { Length: > 0 } normalisert && normalisert == Normaliser(b);

    // Minst ti tegn og høyst 10 % redigeringsavstand. Tall må være like for å skille f.eks. byggetrinn 1 og 2.
    private static bool Lignende(string a, string b)
    {
        a = Normaliser(a); b = Normaliser(b);
        if (Math.Min(a.Length, b.Length) < 10 ||
            new string(a.Where(char.IsDigit).ToArray()) != new string(b.Where(char.IsDigit).ToArray())) return false;
        var grense = Math.Max(a.Length, b.Length) / 10;
        if (Math.Abs(a.Length - b.Length) > grense) return false;
        var forrige = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var rad = new int[b.Length + 1]; rad[0] = i;
            for (var j = 1; j <= b.Length; j++)
                rad[j] = Math.Min(Math.Min(rad[j - 1] + 1, forrige[j] + 1), forrige[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            forrige = rad;
        }
        return forrige[b.Length] <= grense;
    }
}
