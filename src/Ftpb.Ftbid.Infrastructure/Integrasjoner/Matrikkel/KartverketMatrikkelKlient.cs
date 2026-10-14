using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Matrikkel;
using Ftpb.Ftbid.Domain.Feil.Unntak;

namespace Ftpb.Ftbid.Infrastructure.Integrasjoner.Matrikkel;

/// <summary>Bruker Kartverkets SOAP 1.1-kontrakt og HTTPS Basic-autentisering som Nireg.</summary>
public sealed class KartverketMatrikkelKlient(HttpClient http, MatrikkelInnstillinger innstillinger) : IMatrikkelKlient
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Service = "http://matrikkel.statkart.no/matrikkelapi/wsapi/v1/service/matrikkelenhet";
    private static readonly XNamespace Matrikkel = "http://matrikkel.statkart.no/matrikkelapi/wsapi/v1/domain/matrikkelenhet";
    private static readonly XNamespace Domene = "http://matrikkel.statkart.no/matrikkelapi/wsapi/v1/domain";
    private const string SoapHandling = "http://matrikkel.statkart.no/matrikkelapi/wsapi/v1/service/matrikkelenhet/MatrikkelenhetService/findMatrikkelenheterRequest";

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> HentMatrikkelenhetIderAsync(Matrikkeldata matrikkel, CancellationToken avbrudd = default)
    {
        var adresse = HentEndepunkt();
        using var foresporsel = new HttpRequestMessage(HttpMethod.Post, adresse);
        foresporsel.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{innstillinger.Brukernavn}:{innstillinger.Passord}")));
        foresporsel.Headers.Add("SOAPAction", $"\"{SoapHandling}\"");
        foresporsel.Content = new StringContent(LagForesporsel(matrikkel).ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml");
        try
        {
            using var svar = await http.SendAsync(foresporsel, avbrudd);
            if (svar.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new MatrikkelAutentiseringFeiletException();
            if (!svar.IsSuccessStatusCode) throw new MatrikkelOppslagFeiletException();
            return LesIdentifikatorer(await svar.Content.ReadAsStringAsync(avbrudd));
        }
        catch (HttpRequestException feil) { throw new MatrikkelOppslagFeiletException(feil); }
        catch (IOException feil) { throw new MatrikkelOppslagFeiletException(feil); }
        catch (OperationCanceledException feil) when (!avbrudd.IsCancellationRequested)
        { throw new MatrikkelOppslagFeiletException(feil); }
    }

    private Uri HentEndepunkt()
    {
        if (string.IsNullOrWhiteSpace(innstillinger.Brukernavn) || innstillinger.Brukernavn.Contains(':'))
            throw new KonfigurasjonManglerException("Matrikkel:Brukernavn");
        if (string.IsNullOrWhiteSpace(innstillinger.Passord))
            throw new KonfigurasjonManglerException("Matrikkel:Passord");
        if (!Uri.TryCreate(innstillinger.Basisadresse, UriKind.Absolute, out var adresse) || adresse.Scheme != "https" ||
            adresse.UserInfo.Length != 0 || adresse.Query.Length != 0 || adresse.Fragment.Length != 0)
            throw new KonfigurasjonManglerException("Matrikkel:Basisadresse må være en HTTPS-adresse");
        return new Uri(innstillinger.Basisadresse.TrimEnd('/') + "/MatrikkelenhetServiceWS");
    }

    private static XDocument LagForesporsel(Matrikkeldata matrikkel) => new(
        new XElement(Soap + "Envelope", new XAttribute(XNamespace.Xmlns + "s", Soap),
            new XElement(Soap + "Body", new XElement(Service + "findMatrikkelenheter",
                new XElement(Service + "matrikkelenhetsokModel",
                    new XElement(Matrikkel + "kommunenummer", matrikkel.Kommunenummer),
                    new XElement(Matrikkel + "gardsnummer", matrikkel.Gaardsnummer.ToString(CultureInfo.InvariantCulture)),
                    new XElement(Matrikkel + "bruksnummer", matrikkel.Bruksnummer.ToString(CultureInfo.InvariantCulture)),
                    new XElement(Matrikkel + "festenummer", matrikkel.Festenummer),
                    new XElement(Matrikkel + "seksjonsnummer", matrikkel.Seksjonsnummer)),
                new XElement(Service + "matrikkelContext",
                    new XElement(Domene + "locale", "no_NO"),
                    new XElement(Domene + "brukOriginaleKoordinater", true),
                    new XElement(Domene + "koordinatsystemKodeId", new XElement(Domene + "value", 84)),
                    new XElement(Domene + "systemVersion", "trunk"),
                    new XElement(Domene + "klientIdentifikasjon", "Ftpb.Ftbid"),
                    new XElement(Domene + "snapshotVersion", new XElement(Domene + "timestamp", "9999-01-01T00:00:00")))))));

    private static IReadOnlyList<long> LesIdentifikatorer(string innhold)
    {
        try
        {
            using var leser = XmlReader.Create(new StringReader(innhold), new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 30_720_000 });
            var dokument = XDocument.Load(leser);
            var kropp = dokument.Root?.Name == Soap + "Envelope" ? dokument.Root.Element(Soap + "Body") : null;
            if (kropp?.Element(Soap + "Fault") is not null) throw new MatrikkelOppslagFeiletException();
            var respons = kropp?.Elements().SingleOrDefault();
            if (respons?.Name != Service + "findMatrikkelenheterResponse") throw new UgyldigMatrikkelsvarException();
            var resultat = respons.Elements().SingleOrDefault();
            if (resultat?.Name != Service + "return") throw new UgyldigMatrikkelsvarException();
            if ((string?)resultat.Attribute(XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "nil") is "true" or "1")
                throw new UgyldigMatrikkelsvarException();
            var identifikatorer = new List<long>();
            foreach (var element in resultat.Elements())
            {
                var verdi = element.Element(Domene + "value");
                if (element.Name != Matrikkel + "item" || verdi is null || verdi.HasElements || element.Elements().Count() != 1 ||
                    !long.TryParse(verdi.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
                    throw new UgyldigMatrikkelsvarException();
                identifikatorer.Add(id);
            }
            if (!string.IsNullOrWhiteSpace(string.Concat(resultat.Nodes().OfType<XText>().Select(t => t.Value))))
                throw new UgyldigMatrikkelsvarException();
            return identifikatorer.Distinct().ToList();
        }
        catch (XmlException feil) { throw new UgyldigMatrikkelsvarException(feil); }
        catch (InvalidOperationException feil) { throw new UgyldigMatrikkelsvarException(feil); }
    }
}
