using System.Globalization;
using System.ServiceModel;
using Ftpb.Ftbid.Application.Matrikkel;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Infrastructure.Matrikkel.Soap;
using Microsoft.Extensions.Options;

namespace Ftpb.Ftbid.Infrastructure.Matrikkel;

/// <summary>Slår opp eksakt matrikkelenhet og følger alle adresser, veger og postnummerområder.</summary>
public sealed class MatrikkelAdresseoppslag(IOptionsMonitor<MatrikkelInnstillinger> innstillinger) : IMatrikkelAdresseoppslag
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Eiendomsadresse>> HentAsync(Eiendomsidentifikasjon identifikasjon, CancellationToken avbrudd)
    {
        var valg = innstillinger.CurrentValue;
        valg.Valider();

        var binding = new BasicHttpBinding(BasicHttpSecurityMode.Transport)
        {
            MaxReceivedMessageSize = 30_720_000,
            SendTimeout = TimeSpan.FromSeconds(valg.TidsavbruddSekunder),
            OpenTimeout = TimeSpan.FromSeconds(valg.TidsavbruddSekunder)
        };
        binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
        var basis = valg.BasisUrl.TrimEnd('/');
        var enheter = new MatrikkelenhetServiceClient(binding, new EndpointAddress(basis + "/MatrikkelenhetServiceWS"));
        var adresser = new AdresseServiceClient(binding, new EndpointAddress(basis + "/AdresseServiceWS"));
        var lager = new StoreServiceClient(binding, new EndpointAddress(basis + "/StoreServiceWS"));
        SettLegitimasjon(enheter, valg);
        SettLegitimasjon(adresser, valg);
        SettLegitimasjon(lager, valg);
        using var registrering = avbrudd.Register(() => { enheter.Abort(); adresser.Abort(); lager.Abort(); });
        try
        {
            avbrudd.ThrowIfCancellationRequested();
            var kontekst = new MatrikkelContext
            {
                locale = "no_NO", brukOriginaleKoordinater = true,
                koordinatsystemKodeId = new KoordinatsystemKodeId { value = 84 },
                systemVersion = "trunk", klientIdentifikasjon = "Ftpb.Ftbid",
                snapshotVersion = new Timestamp { timestamp = new DateTime(9999, 1, 1) }
            };
            var id = await enheter.findMatrikkelenhetAsync(LagIdent(identifikasjon), kontekst).WaitAsync(avbrudd);
            if (id is null) return [];
            var treff = await adresser.findAdresserForMatrikkelenhetAsync(id, kontekst).WaitAsync(avbrudd);
            var adresseIds = treff.@return ?? throw new InvalidOperationException("Matrikkel returnerte ingen adresseliste.");
            var adresseobjekter = await HentObjekterAsync(lager, adresseIds, kontekst, avbrudd);
            var alleAdresser = adresseobjekter.Select(o => o as Adresse ??
                throw new InvalidOperationException("Matrikkel returnerte et ukjent adresseobjekt.")).ToArray();
            var referanser = alleAdresser.OfType<Vegadresse>().Select(a => (MatrikkelBubbleId)a.vegId)
                .Concat(alleAdresser.SelectMany(a => a.kretsIds ?? []).Cast<MatrikkelBubbleId>()).ToArray();
            var objekter = await HentObjekterAsync(lager, referanser, kontekst, avbrudd);
            var veger = objekter.OfType<Veg>().ToDictionary(v => v.id.value);
            var postomrader = objekter.OfType<Postnummeromrade>().ToDictionary(p => p.id.value);
            return alleAdresser.OrderBy(a => a.id.value).Select(a => TilAdresse(a, identifikasjon, veger, postomrader)).ToArray();
        }
        finally
        {
            // Abort frigjør også feilslåtte kanaler uten at Close skjuler den opprinnelige feilen.
            enheter.Abort();
            adresser.Abort();
            lager.Abort();
        }
    }

    internal static MatrikkelenhetIdent LagIdent(Eiendomsidentifikasjon ident) => new()
    {
        kommuneIdent = new KommuneIdent { kommunenummer = ident.Kommunenummer },
        gardsnummer = ident.Gaardsnummer, gardsnummerSpecified = true,
        bruksnummer = ident.Bruksnummer, bruksnummerSpecified = true,
        festenummer = ident.Festenummer, festenummerSpecified = true,
        seksjonsnummer = ident.Seksjonsnummer, seksjonsnummerSpecified = true
    };

    private static void SettLegitimasjon<T>(ClientBase<T> klient, MatrikkelInnstillinger valg) where T : class
    {
        klient.ClientCredentials.UserName.UserName = valg.Brukernavn;
        klient.ClientCredentials.UserName.Password = valg.Passord;
    }

    private static async Task<MatrikkelBubbleObject[]> HentObjekterAsync(StoreServiceClient lager,
        IEnumerable<MatrikkelBubbleId> ids, MatrikkelContext kontekst, CancellationToken avbrudd)
    {
        var resultat = new List<MatrikkelBubbleObject>();
        foreach (var del in ids.DistinctBy(i => i.value).Chunk(100))
        {
            var svar = await lager.getObjectsAsync(del, kontekst).WaitAsync(avbrudd);
            if (svar.@return is null || svar.@return.Length != del.Length)
                throw new InvalidOperationException("Matrikkel returnerte et ufullstendig objektsett.");
            resultat.AddRange(svar.@return);
        }
        return resultat.ToArray();
    }

    internal static Eiendomsadresse TilAdresse(Adresse adresse, Eiendomsidentifikasjon ident,
        IReadOnlyDictionary<long, Veg> veger, IReadOnlyDictionary<long, Postnummeromrade> postomrader)
    {
        string? gatenavn = null, husnr = null, bokstav = null;
        string adresselinje;
        switch (adresse)
        {
            case Vegadresse vegadresse:
                gatenavn = veger[vegadresse.vegId.value].adressenavn;
                husnr = vegadresse.nummerSpecified ? vegadresse.nummer.ToString(CultureInfo.InvariantCulture) : null;
                bokstav = Tekst(vegadresse.bokstav);
                adresselinje = $"{gatenavn} {husnr}{bokstav}".Trim();
                break;
            case Matrikkeladresse matrikkeladresse:
                adresselinje = $"{ident.Gaardsnummer}/{ident.Bruksnummer}/{ident.Festenummer}";
                if (matrikkeladresse.undernummerSpecified) adresselinje += $"-{matrikkeladresse.undernummer}";
                break;
            default:
                throw new InvalidOperationException("Ukjent adressetype fra Matrikkel.");
        }
        var post = (adresse.kretsIds ?? []).Select(k => postomrader.GetValueOrDefault(k.value)).FirstOrDefault(p => p is not null);
        return new Eiendomsadresse
        {
            Adresselinje1 = adresselinje, Adresselinje2 = Tekst(adresse.adressetilleggsnavn),
            Gatenavn = Tekst(gatenavn), Husnr = husnr, Bokstav = bokstav, Landkode = "NO",
            Postnr = post?.kretsnummer.ToString("D4", CultureInfo.InvariantCulture), Poststed = Tekst(post?.kretsnavn)
        };
    }

    private static string? Tekst(string? verdi) => string.IsNullOrWhiteSpace(verdi) ? null : verdi.Trim();
}
