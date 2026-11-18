using System.Globalization;
using System.ServiceModel;
using Ftpb.Ftbid.Application.Matrikkel;
using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Infrastructure.Matrikkel.Soap;
using Microsoft.Extensions.Options;

namespace Ftpb.Ftbid.Infrastructure.Matrikkel;

/// <summary>Slår opp eksakt matrikkelenhet og følger alle adresser, veger og postnummerområder.</summary>
public sealed class MatrikkelAdresseoppslag(IOptionsMonitor<MatrikkelOptions> settings) : IMatrikkelAdresseoppslag
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Eiendomsadresse>> GetAsync(Eiendomsidentifikasjon identifikasjon, CancellationToken cancellationToken)
    {
        var options = settings.CurrentValue;
        options.Validate();

        var binding = new BasicHttpBinding(BasicHttpSecurityMode.Transport)
        {
            MaxReceivedMessageSize = 30_720_000,
            SendTimeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
            OpenTimeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };
        binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
        var basis = options.BaseUrl.TrimEnd('/');
        var enheter = new MatrikkelenhetServiceClient(binding, new EndpointAddress(basis + "/MatrikkelenhetServiceWS"));
        var adresser = new AdresseServiceClient(binding, new EndpointAddress(basis + "/AdresseServiceWS"));
        var bygninger = new BygningServiceClient(binding, new EndpointAddress(basis + "/BygningServiceWS"));
        var repository = new StoreServiceClient(binding, new EndpointAddress(basis + "/StoreServiceWS"));
        SetCredentials(enheter, options);
        SetCredentials(adresser, options);
        SetCredentials(bygninger, options);
        SetCredentials(repository, options);
        using var registration = cancellationToken.Register(() => { enheter.Abort(); adresser.Abort(); bygninger.Abort(); repository.Abort(); });
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = new MatrikkelContext
            {
                locale = "no_NO", brukOriginaleKoordinater = true,
                koordinatsystemKodeId = new KoordinatsystemKodeId { value = 84 },
                systemVersion = "trunk", klientIdentifikasjon = "Ftpb.Ftbid",
                snapshotVersion = new Timestamp { timestamp = new DateTime(9999, 1, 1) }
            };
            var id = await enheter.findMatrikkelenhetAsync(LagIdent(identifikasjon), context).WaitAsync(cancellationToken);
            if (id is null) return [];
            var adresseIds = await HentAlleAdresseIdsAsync(
                async () => (await adresser.findAdresserForMatrikkelenhetAsync(id, context).WaitAsync(cancellationToken)).@return,
                async () => (await bygninger.findByggForMatrikkelenhetAsync(id, context).WaitAsync(cancellationToken)).@return,
                async byggIds => (await adresser.findAdresserForByggListAsync(byggIds, context).WaitAsync(cancellationToken)).@return);
            var adresseobjekter = await GetObjectsAsync(repository, adresseIds, context, cancellationToken);
            var alleAdresser = adresseobjekter.Select(o => o as Adresse ??
                throw new InvalidOperationException("Matrikkel returnerte et ukjent adresseobjekt.")).ToArray();
            var referanser = alleAdresser.OfType<Vegadresse>().Select(a => (MatrikkelBubbleId)a.vegId)
                .Concat(alleAdresser.SelectMany(a => a.kretsIds ?? []).Cast<MatrikkelBubbleId>())
                .Concat(alleAdresser.OfType<Matrikkeladresse>().Select(a => (MatrikkelBubbleId)a.matrikkelenhetId)).ToArray();
            var objekter = await GetObjectsAsync(repository, referanser, context, cancellationToken);
            var veger = objekter.OfType<Veg>().ToDictionary(v => v.id.value);
            var postomrader = objekter.OfType<Postnummeromrade>().ToDictionary(p => p.id.value);
            var adresseenheter = objekter.OfType<Matrikkelenhet>().ToDictionary(e => e.id.value);
            return alleAdresser.OrderBy(a => a.id.value).Select(a =>
            {
                // En adresse via et bygg kan ha sin direkte matrikkeltilknytning på en annen eiendom.
                var adresseident = identifikasjon;
                if (a is Matrikkeladresse)
                {
                    var nummer = adresseenheter[a.matrikkelenhetId.value].matrikkelnummer;
                    adresseident = adresseident with { Gaardsnummer = nummer.gardsnummer,
                        Bruksnummer = nummer.bruksnummer, Festenummer = nummer.festenummer };
                }
                return TilAdresse(a, adresseident, veger, postomrader);
            }).ToArray();
        }
        finally
        {
            // Abort frigjør også feilslåtte kanaler uten at Close skjuler den opprinnelige feilen.
            enheter.Abort();
            adresser.Abort();
            bygninger.Abort();
            repository.Abort();
        }
    }

    /// <summary>Forener direkte adresser med adresser koblet via eiendommens bygg og bruksenheter.</summary>
    internal static async Task<AdresseId[]> HentAlleAdresseIdsAsync(Func<Task<AdresseId[]>> hentDirekte,
        Func<Task<ByggId[]>> hentBygg, Func<ByggId[], Task<ByggIdTilAdresseIdsMapEntry[]>> hentByggAdresser)
    {
        var direkte = await hentDirekte() ?? throw new InvalidOperationException("Matrikkel returnerte ingen adresseliste.");
        var bygg = await hentBygg() ?? throw new InvalidOperationException("Matrikkel returnerte ingen bygningsliste.");
        var result = new List<AdresseId>(direkte);
        foreach (var chunk in bygg.DistinctBy(b => b.value).Chunk(100))
        {
            var response = await hentByggAdresser(chunk) ?? throw new InvalidOperationException("Matrikkel returnerte ingen bygningsadresser.");
            var received = response.Select(s => s.key.value).ToHashSet();
            if (chunk.Any(b => !received.Contains(b.value)) || response.Any(s => s.value is null))
                throw new InvalidOperationException("Matrikkel returnerte et ufullstendig sett med bygningsadresser.");
            result.AddRange(response.SelectMany(s => s.value));
        }
        return result.DistinctBy(a => a.value).OrderBy(a => a.value).ToArray();
    }

    internal static MatrikkelenhetIdent LagIdent(Eiendomsidentifikasjon ident) => new()
    {
        kommuneIdent = new KommuneIdent { kommunenummer = ident.Kommunenummer },
        gardsnummer = ident.Gaardsnummer, gardsnummerSpecified = true,
        bruksnummer = ident.Bruksnummer, bruksnummerSpecified = true,
        festenummer = ident.Festenummer, festenummerSpecified = true,
        seksjonsnummer = ident.Seksjonsnummer, seksjonsnummerSpecified = true
    };

    private static void SetCredentials<T>(ClientBase<T> client, MatrikkelOptions options) where T : class
    {
        client.ClientCredentials.UserName.UserName = options.Username;
        client.ClientCredentials.UserName.Password = options.Password;
    }

    private static async Task<MatrikkelBubbleObject[]> GetObjectsAsync(StoreServiceClient repository,
        IEnumerable<MatrikkelBubbleId> ids, MatrikkelContext context, CancellationToken cancellationToken)
    {
        var result = new List<MatrikkelBubbleObject>();
        foreach (var chunk in ids.DistinctBy(i => i.value).Chunk(100))
        {
            var response = await repository.getObjectsAsync(chunk, context).WaitAsync(cancellationToken);
            if (response.@return is null || response.@return.Length != chunk.Length)
                throw new InvalidOperationException("Matrikkel returnerte et ufullstendig objektsett.");
            result.AddRange(response.@return);
        }
        return result.ToArray();
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
                bokstav = Text(vegadresse.bokstav);
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
            Adresselinje1 = adresselinje, Adresselinje2 = Text(adresse.adressetilleggsnavn),
            Gatenavn = Text(gatenavn), Husnr = husnr, Bokstav = bokstav, Landkode = "NO",
            Postnr = post?.kretsnummer.ToString("D4", CultureInfo.InvariantCulture), Poststed = Text(post?.kretsnavn)
        };
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
