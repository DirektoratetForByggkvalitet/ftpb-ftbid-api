using System.Net;
using System.Text;
using System.Xml.Linq;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Domain.Feil.Unntak;
using Ftpb.Ftbid.Infrastructure.Integrasjoner.Matrikkel;
using Xunit;

namespace Ftpb.Ftbid.Tests.Enhet;

/// <summary>Kontrollerer SOAP-kontrakten fra Nireg, autentisering og sikker tolkning av Kartverkets svar.</summary>
public sealed class KartverketMatrikkelKlientTester
{
    private const string Service = "http://matrikkel.statkart.no/matrikkelapi/wsapi/v1/service/matrikkelenhet";
    private const string Matrikkel = "http://matrikkel.statkart.no/matrikkelapi/wsapi/v1/domain/matrikkelenhet";
    private const string Domene = "http://matrikkel.statkart.no/matrikkelapi/wsapi/v1/domain";
    private const string Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly Matrikkeldata Nummer = new("0301", 10, 20, 0, 0);

    private static string Svar(string resultat) => $"""
        <s:Envelope xmlns:s="{Soap}" xmlns:m="{Service}" xmlns:me="{Matrikkel}" xmlns:d="{Domene}">
        <s:Body><m:findMatrikkelenheterResponse><m:return>{resultat}</m:return></m:findMatrikkelenheterResponse></s:Body>
        </s:Envelope>
        """;

    private static MatrikkelInnstillinger Innstillinger() => new() { Brukernavn = "testbruker", Passord = "testpassord" };
    private static HttpResponseMessage Respons(string innhold, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(innhold, Encoding.UTF8, "text/xml") };

    /// <summary>Sender Basic over HTTPS, korrekt SOAP-handling og eksakte felter, også feste/seksjon lik null.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 4)]
    public async Task ForesporselenSamsvarerMedNiregsKontrakt(int feste, int seksjon)
    {
        using var handler = new TestHandler(async (foresporsel, avbrudd) =>
        {
            Assert.Equal("https://nd.matrikkel.no/matrikkelapi/wsapi/v1/MatrikkelenhetServiceWS", foresporsel.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Post, foresporsel.Method);
            Assert.Equal("Basic", foresporsel.Headers.Authorization!.Scheme);
            Assert.Equal("testbruker:testpassord", Encoding.UTF8.GetString(Convert.FromBase64String(foresporsel.Headers.Authorization.Parameter!)));
            Assert.Equal($"\"{Service}/MatrikkelenhetService/findMatrikkelenheterRequest\"", Assert.Single(foresporsel.Headers.GetValues("SOAPAction")));
            Assert.Equal("text/xml", foresporsel.Content!.Headers.ContentType!.MediaType);
            var dokument = XDocument.Parse(await foresporsel.Content.ReadAsStringAsync(avbrudd));
            var operasjon = dokument.Root!.Element(XName.Get("Body", Soap))!.Element(XName.Get("findMatrikkelenheter", Service))!;
            var modell = operasjon.Element(XName.Get("matrikkelenhetsokModel", Service))!;
            Assert.Equal(["kommunenummer", "gardsnummer", "bruksnummer", "festenummer", "seksjonsnummer"], modell.Elements().Select(e => e.Name.LocalName));
            Assert.All(modell.Elements(), e => Assert.Equal(Matrikkel, e.Name.NamespaceName));
            Assert.Equal(["0301", "10", "20", feste.ToString(), seksjon.ToString()], modell.Elements().Select(e => e.Value));
            var kontekst = operasjon.Element(XName.Get("matrikkelContext", Service))!;
            Assert.Equal("no_NO", kontekst.Element(XName.Get("locale", Domene))!.Value);
            Assert.Equal("84", kontekst.Element(XName.Get("koordinatsystemKodeId", Domene))!.Element(XName.Get("value", Domene))!.Value);
            Assert.Equal("9999-01-01T00:00:00", kontekst.Element(XName.Get("snapshotVersion", Domene))!.Element(XName.Get("timestamp", Domene))!.Value);
            return Respons(Svar("<me:item><d:value>12345</d:value></me:item>"));
        });
        using var http = new HttpClient(handler);
        var klient = new KartverketMatrikkelKlient(http, Innstillinger());
        Assert.Equal([12345L], await klient.HentMatrikkelenhetIderAsync(Nummer with { Festenummer = feste, Seksjonsnummer = seksjon }));
    }

    /// <summary>Et gyldig, tomt søkeresultat skilles fra et ugyldig svar.</summary>
    [Fact]
    public async Task TomtSokeresultatGirIngenIdentifikatorer()
    {
        using var http = new HttpClient(new TestHandler((_, _) => Task.FromResult(Respons(Svar("")))));
        Assert.Empty(await new KartverketMatrikkelKlient(http, Innstillinger()).HentMatrikkelenhetIderAsync(Nummer));
    }

    /// <summary>HTML, ugyldig XML, feil navnerom og ugyldige id-er kan aldri godkjenne en matrikkelenhet.</summary>
    [Theory]
    [InlineData("<html>Innlogging</html>")]
    [InlineData("<ugyldig")]
    [InlineData("<item><value>12345</value></item>")]
    [InlineData("<me:item><d:value>0</d:value></me:item>")]
    [InlineData("<me:item><d:value>tekst</d:value></me:item>")]
    [InlineData("<me:item/>")]
    [InlineData("<me:item><d:value><me:ukjent>12345</me:ukjent></d:value></me:item>")]
    [InlineData("<me:item><d:value>12345</d:value><d:value>0</d:value></me:item>")]
    [InlineData("uventet tekst")]
    public async Task UgyldigeResultaterAvvises(string resultat)
    {
        using var http = new HttpClient(new TestHandler((_, _) => Task.FromResult(Respons(Svar(resultat)))));
        await Assert.ThrowsAsync<UgyldigMatrikkelsvarException>(() =>
            new KartverketMatrikkelKlient(http, Innstillinger()).HentMatrikkelenhetIderAsync(Nummer));
    }

    /// <summary>SOAP-fault behandles som servicefeil, aldri som et tomt søketreff.</summary>
    [Fact]
    public async Task SoapFaultAvvises()
    {
        var innhold = $"<s:Envelope xmlns:s='{Soap}'><s:Body><s:Fault><faultcode>s:Server</faultcode></s:Fault></s:Body></s:Envelope>";
        using var http = new HttpClient(new TestHandler((_, _) => Task.FromResult(Respons(innhold))));
        await Assert.ThrowsAsync<MatrikkelOppslagFeiletException>(() =>
            new KartverketMatrikkelKlient(http, Innstillinger()).HentMatrikkelenhetIderAsync(Nummer));
    }

    /// <summary>Avvist Basic-innlogging får egen feiltype; andre HTTP-feil er servicefeil.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    [InlineData(HttpStatusCode.Forbidden, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public async Task HttpFeilKanIkkeGiGodkjentMatrikkel(HttpStatusCode status, bool autentisering)
    {
        using var http = new HttpClient(new TestHandler((_, _) => Task.FromResult(Respons("", status))));
        var klient = new KartverketMatrikkelKlient(http, Innstillinger());
        if (autentisering) await Assert.ThrowsAsync<MatrikkelAutentiseringFeiletException>(() => klient.HentMatrikkelenhetIderAsync(Nummer));
        else await Assert.ThrowsAsync<MatrikkelOppslagFeiletException>(() => klient.HentMatrikkelenhetIderAsync(Nummer));
    }

    /// <summary>Nettverksfeil og tidsavbrudd stopper validering og videresendes ikke som godkjente matrikler.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TransportfeilAvvises(bool tidsavbrudd)
    {
        using var http = new HttpClient(new TestHandler((_, _) => tidsavbrudd
            ? Task.FromException<HttpResponseMessage>(new TaskCanceledException())
            : Task.FromException<HttpResponseMessage>(new HttpRequestException())));
        await Assert.ThrowsAsync<MatrikkelOppslagFeiletException>(() =>
            new KartverketMatrikkelKlient(http, Innstillinger()).HentMatrikkelenhetIderAsync(Nummer));
    }

    /// <summary>Brukeravbrudd bevares slik at et avbrutt kall ikke behandles som en Kartverket-feil.</summary>
    [Fact]
    public async Task AvbruddBevares()
    {
        using var kilde = new CancellationTokenSource();
        kilde.Cancel();
        using var http = new HttpClient(new TestHandler((_, ct) => Task.FromCanceled<HttpResponseMessage>(ct)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new KartverketMatrikkelKlient(http, Innstillinger()).HentMatrikkelenhetIderAsync(Nummer, kilde.Token));
    }

    /// <summary>Manglende innlogging og HTTP-adresser avvises før noe sendes til Kartverket.</summary>
    [Theory]
    [InlineData("", "testpassord", "https://nd.matrikkel.no/")]
    [InlineData("testbruker", "", "https://nd.matrikkel.no/")]
    [InlineData("testbruker", "testpassord", "http://nd.matrikkel.no/")]
    public async Task UfullstendigKonfigurasjonAvvises(string bruker, string passord, string adresse)
    {
        using var http = new HttpClient(new TestHandler((_, _) => throw new InvalidOperationException("Ingen HTTP-forespørsel skal sendes.")));
        var valg = new MatrikkelInnstillinger { Brukernavn = bruker, Passord = passord, Basisadresse = adresse };
        await Assert.ThrowsAsync<KonfigurasjonManglerException>(() => new KartverketMatrikkelKlient(http, valg).HentMatrikkelenhetIderAsync(Nummer));
    }

    private sealed class TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> svar) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage foresporsel, CancellationToken avbrudd) => svar(foresporsel, avbrudd);
    }
}
