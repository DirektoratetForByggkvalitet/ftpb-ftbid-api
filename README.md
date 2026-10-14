# ftpb-ftbid-api

Selvstendig .NET 10 API med PostgreSQL med Maskinporten-autentisering etter mønsteret i NIREG.

## Lokal oppstart

Forutsetning for containerdrift: Docker med Compose. .NET 10 SDK trengs bare ved direkte kjøring og testing på vertsmaskinen. Kjør kommandoene fra denne mappen.

```sh
docker compose up -d --build --wait
```

Compose starter både API-et og PostgreSQL. API-et kjører i `Local`, venter på en frisk database og bruker servicenavnet `postgres` som databasevert. Migrasjoner kjøres ved oppstart. Det eksisterende PostgreSQL-volumet og importerte data gjenbrukes.

For en ny, tom database kan eksemplenes Oslo-kommune og arkivreferanse importeres slik:

```sh
docker compose exec api dotnet Ftpb.Ftbid.Api.dll --importer /data/referansedata.eksempel.json
```

Se logger, bygg på nytt etter kodeendringer eller stopp containerne:

```sh
docker compose logs -f api
docker compose up -d --build --wait
docker compose down
```

`down` beholder databasevolumet. API-et kjører uten administratorrettigheter og har en helsesjekk mot `/helse`. Begge containere starter igjen etter omstart av Docker, med mindre de er stoppet manuelt. Lokale CSV-uttrekk, sikkerhetskopier, tester og IDE-filer inngår ikke i Docker-byggekonteksten.

Hvis du vil kjøre API-et direkte med .NET under utvikling, stopp API-containeren først for å frigjøre port 5080:

```sh
docker compose stop api
docker compose up -d --wait postgres
dotnet restore
dotnet run --project src/Ftpb.Ftbid.Api
```

API-et kjører på `http://localhost:5080`. Swagger UI finnes på `/swagger`, OpenAPI-dokumentet på `/openapi/v1.json`, databaseberedskap på `/helse` og prosesshelse på `/helse/levende`. Databaseberedskap kontrollerer tilkobling og ftbid-tabellen og returnerer 503 ved databasefeil. Prosesshelse kontrollerer at API-prosessen svarer.

API-containerens port 8080 publiseres som `localhost:5080`. PostgreSQL og API-et kommuniserer på Compose-nettverket.

Compose oppretter databasen `ftbid` på `localhost:55432`, med brukernavn `ftbid` og lokalt passord `ftbid_lokal`. Porten bindes bare til lokal maskin. Data beholdes i et Docker-volum. Eksempelfilen inneholder **kun Oslo og én eksempelarkivreferanse**; den er ikke et komplett kommuneregister. Importer egne referansedata før reell bruk.

## Prosjekter og ansvar

| Prosjekt | Ansvar |
| --- | --- |
| Api | HTTP-kontrakter, Maskinporten, scopes, ratebegrensning, helse og Swagger |
| Domain | Ftbid, adresser, matrikkelopplysninger, identifikasjonsmetadata, EAN og regelunntak |
| Application | Grensesnitt, validering, forretningsregler og resultatmodeller |
| Infrastructure | PostgreSQL, EF Core, migrasjoner og referansedataimport |
| Tests | xUnit-enhetstester og HTTP-integrasjonstester med ekte PostgreSQL |

Avhengighetene går fra Api til Application og Infrastructure, fra Infrastructure til Application og Domain, og fra Application til Domain. Domain har ingen eksterne pakkeavhengigheter. Offentlige metoder har vanlige `///`-kodedokumentasjonskommentarer eller arver dokumentasjonen fra grensesnittet. Separate XML-dokumentasjonsfiler genereres ikke.

### Mapper og servicer

Hver modell og kontrakt ligger i en egen fil. Mappene og navnerommene beskriver ansvaret:

| Prosjekt | Mapper |
| --- | --- |
| Api | `Kontrollere`, `Autentisering`, `Feilhandtering`, `Helse`, `Konfigurasjon` |
| Domain | `Prosjekter`, `Referansedata`, `Identifikatorer`, `Feil/Unntak` |
| Application | `Kontrakter`, `Services`, `Lagring`, `Validering`, `Matrikkel`, `Konfigurasjon`, `Feil` |
| Infrastructure | `Persistens/Lagre`, `Referansedata`, `Integrasjoner/Matrikkel`, `Migrasjoner` |
| Tests | `Enhet`, `Integrasjon`, `Oppsett` |

`FtbidOpprettingService` validerer prosjektdata og oppretter en identifikator. `FtbidDublettService` vurderer dubletter og sannsynlige dubletter før lagring, under PostgreSQL-transaksjonslåsen. `FtbidOppslagService` håndterer søk, oppslag og resultatmodeller. `FtbidTilknytningService` håndterer arkivreferanser. `FtbidEiendomService` validerer tillegg og sletting av adresser og matrikkelenheter, med atomisk lagring gjennom `IFtbidEiendomslager` og `PostgresFtbidEiendomslager`. Servicene har egne grensesnitt som kontrolleren får gjennom avhengighetsinjeksjon.

`IFtbidLager` samler PostgreSQL-operasjonene for prosjektet og dets tilknytninger. Opprettingens transaksjon, sekvenstildeling og duplikatbeskyttelse ligger samlet i `PostgresFtbidLager`. Kommuneoppslag har en egen kontrakt, `IKommuneregister`, og implementeres av `PostgresKommuneregister`. `IReferansedataImportService` brukes av importkommandoen og implementeres i Infrastructure, med atomisk oppdatering av kommuner og skjemaer.

Felles tekstvalidering og normalisering er interne hjelpefunksjoner i Application. De trenger ingen egen injisert service. Serviceregistreringen er samlet i `Api/Konfigurasjon/Serviceregistrering.cs`. API-kontrakten og databaseskjemaet er uendret av denne oppdelingen.

## Endepunkter

| Metode | Sti | Scope | Svar ved suksess |
| --- | --- | --- | --- |
| POST | `/api/ftbid` | `dibk:bygg/ftbid.endre` | 200 med ftbid som JSON-streng; 409 ved dublett |
| POST | `/api/ftbid/tilknytt` | `dibk:bygg/ftbid.endre` | 204 |
| GET | `/api/ftbid/sok` | `dibk:bygg/ftbid.les` | 200 med liste av prosjekter og metadata |
| GET | `/api/ftbid/{ftbid}` | `dibk:bygg/ftbid.les` | 200 med liste, tom ved ukjent ftbid |
| POST | `/api/ftbid/{ftbid}/adresser` | `dibk:bygg/ftbid.endre` | 204 |
| DELETE | `/api/ftbid/{ftbid}/adresser?adresse=...` | `dibk:bygg/ftbid.endre` | 204 |
| POST | `/api/ftbid/{ftbid}/matrikkelopplysninger` | `dibk:bygg/ftbid.endre` | 204 |
| DELETE | `/api/ftbid/{ftbid}/matrikkelopplysninger?kommunenummer=...&gaardsnummer=...&bruksnummer=...` | `dibk:bygg/ftbid.endre` | 204 |

API-ruter og JSON-feltnavn er på norsk. Tilknytning bruker `Ftbid` og `Arkivreferanse`, og svarene bruker `TilknyttedeSkjemaer` med `Arkivreferanse`, `Skjematype` og `Tiltakstyper`. JSON-inndata er ikke følsomme for store og små bokstaver. Dette er en bevisst endring fra adapterens engelske ruter og JSON-feltnavn; klienter må oppdateres til den nye kontrakten.

### Oppretting med norske feltnavn og nye lister

```json
{
  "Kommunenummer": "0301",
  "Gaardsnummer": 10,
  "Bruksnummer": 1,
  "Festenummer": 0,
  "Seksjonsnummer": 0,
  "Adresse": "Karl Johans gate 1",
  "Prosjektnavn": "Utvidelse av bygget",
  "Prosjektnr": "P-1001",
  "System": "Søknadssystem",
  "Adresser": ["Storgata 2"],
  "Matrikkelopplysninger": [
    { "Kommunenummer": "0301", "Gaardsnummer": 20, "Bruksnummer": 2, "Festenummer": 3, "Seksjonsnummer": 4 }
  ]
}
```

De eksisterende matrikkelfeltene er hovedmatrikkelenheten og legges først i listen. Enkeltadressen legges først i adresselisten. Du kan også bruke kun `Adresser` og `Matrikkelopplysninger`; første verdi blir da hovedverdien. Like verdier fjernes, og rekkefølgen beholdes. Maksimalt 100 adresser og 100 matrikkelopplysninger tillates. Kommuner må være registrert og gyldige på opprettingstidspunktet. Hver matrikkelenhet må i tillegg få et eksakt treff hos Kartverket før oppretting eller tilknytning.

Oppretting støtter også de valgfrie feltene `EksternProsjektreferanse` (200 tegn), `KommunaltSaksnummer` (200), `Tiltakshaver` (500), `Organisasjonsnummer` (9), `Bygningsnummer` (50) og `Tiltakstype` (200). Feltene lagres og returneres ved oppslag og søk. `Prosjektnavn` brukes også som tiltaksnavn. Det eksisterende `Prosjektnr` beholdes som reserve for ekstern referanse når `EksternProsjektreferanse` mangler.

Dublettkontrollen krever minst én felles kommune og bruker følgende regler:

- **Dublett (`V0067`)**: samme kommunale saksnummer i hovedkommunen, samme eksterne referanse i samme system og hovedkommune, eller samme normaliserte prosjektnavn og minst én felles fullstendig matrikkelenhet. Feste- og seksjonsnummer inngår; også sekundærmatrikler kontrolleres.
- **Sannsynlig dublett (`V0068`)**: stedstreff på full matrikkel, adresse eller bygningsnummer, sammen med samme eller nesten samme prosjektnavn. Et navn på minst ti tegn anses som nesten likt ved høyst 10 % redigeringsavstand; siffersekvensen må være identisk for å skille ulike byggetrinn. Stedstreff sammen med minst to av tiltakshaver/organisasjonsnummer, bygningsnummer og tiltakstype gir også et sannsynlig treff. Tiltakshaver og organisasjonsnummer teller samlet som ett signal.

Tekst sammenlignes uten hensyn til store/små bokstaver og med normaliserte mellomrom og Unicode. Tomme verdier gir aldri treff. Kommune, adresse, tiltakshaver eller tiltakstype alene blokkerer ikke oppretting. Regler for sannsynlige dubletter er heuristikker, ikke statistiske sannsynligheter. Ved flere treff prioriteres dubletter foran sannsynlige dubletter, deretter flest samsvarende felter og ftbid i stigende rekkefølge.

Begge typer treff avviser opprettingen med **409 Conflict**, `Location: /api/ftbid/{eksisterendeFtbid}` og ProblemDetails. Svaret inneholder `ftbidFeil.Kode`, `eksisterendeFtbid`, `sannsynligDublett` og `samsvarendeFelter`, slik at klienten kan forklare treffet og hente prosjektet. `Location` inkluderer eventuell `PathBase`. Eksisterende prosjekt endres ikke; prosjektnummer etterfylles ikke lenger. Dette endrer kontrakten for gjentatt oppretting fra 200 til 409. Samtidige opprettinger beskyttes av den eksisterende PostgreSQL-transaksjonslåsen på tvers av serviceinstanser, og en unik indeks på full hovedmatrikkel og normalisert prosjektnavn. Migrationen `LeggTilDublettopplysninger` legger til de nye feltene og oppdaterer indeksen.


EAN-formatet er sju sifre GS1-prefiks, tre reserverte nuller, sju sifre PostgreSQL-sekvens og ett kontrollsiffer. Grunnkonfigurasjonen beholder adapterens eksempelprefiks `7099999`. Lokal utvikling bruker `7073387`, som samsvarer med det importerte v1-uttrekket. Prefikset kan endres med `Ftbid__Gs1Prefiks`; bruk samme prefiks som datagrunnlaget for kortoppslag. Sekvensen kan ikke gå rundt og gjenbruke identifikatorer.

### Tilknytning og oppslag

```json
{ "Ftbid": "707338700000000014", "Arkivreferanse": "AR123456789" }
```

Arkivreferansen må finnes i `skjemametadata`, og ftbid må finnes i `ftbid`. En arkivreferanse kan være tilknyttet én ftbid om gangen. Gjentatt tilknytning er idempotent; tilknytning til en annen ftbid flytter referansen. Full ftbid og de siste åtte sifrene kan brukes til oppslag; kortformen utvides med konfigurert GS1-prefiks og tre nuller.

### Legg til og fjern adresser og matrikkelopplysninger

Endringene gjelder en eksisterende ftbid og støtter både full identifikator og de siste åtte sifrene. Alle fire operasjoner krever endrescope, med samme lokale tokenregel som resten av API-et. Vellykkede endringer returnerer 204 uten innhold. Ukjent ftbid gir 400 med regelkode `V0054`.

Legg til én adresse eller matrikkelenhet per kall:

```text
POST /api/ftbid/{ftbid}/adresser
{ "Adresse": "Storgata 3" }

POST /api/ftbid/{ftbid}/matrikkelopplysninger
{ "Kommunenummer": "0301", "Gaardsnummer": 30, "Bruksnummer": 3, "Festenummer": 0, "Seksjonsnummer": 2 }
```

Sletting identifiserer verdien gjennom søkeparametere, uten forespørselskropp. Adressen må URL-kodes. Matrikkelenheten identifiseres av alle fem feltene; feste- og seksjonsnummer er 0 hvis de utelates. Kommune, gårdsnummer og bruksnummer kreves.

```text
DELETE /api/ftbid/{ftbid}/adresser?adresse=Storgata%203
DELETE /api/ftbid/{ftbid}/matrikkelopplysninger?kommunenummer=0301&gaardsnummer=30&bruksnummer=3&festenummer=0&seksjonsnummer=2
```

Tillegg av en eksisterende verdi og sletting av en verdi som allerede er borte returnerer også 204. Adressetekst trimmes og sammenlignes uten hensyn til store og små bokstaver. Matrikkelenheter sammenlignes på alle fem feltene. Nye verdier legges sist; maksimalt 100 adresser og 100 matrikkelenheter tillates. Ny matrikkel krever en gyldig kommune og et eksakt treff i Kartverkets Matrikkel. Sletting kontrollerer formatet, men krever ikke at historiske kommunenummer er gyldige i dagens register.

Neste gjenværende verdi blir hovedverdi hvis hovedadressen eller hovedmatrikkelen fjernes. Alle adresser kan fjernes, slik at hovedadressen blir null. Minst én matrikkelenhet må beholdes; sletting av den siste gir `V0061`. Ftbid, prosjektnavn og tilknyttede skjemaer bevares.

Ved bytte av hovedmatrikkel oppdateres også hovedfeltene og den unike prosjektidentiteten i `ftbid`. Hvis dette ville gitt samme kommune, gårdsnummer, bruksnummer, festenummer, seksjonsnummer og normaliserte prosjektnavn som et annet prosjekt, avvises hele endringen med `V0062`. Alle endringer bruker samme transaksjonslås som oppretting og v1-import, slik at samtidige kall ikke mister data eller omgår listegrenser.

### Søk

Søkeparametrene er `kommunenummer`, `gaardsnummer`, `bruksnummer`, `prosjektnavn` og `adresse`. Minst én parameter kreves. Matrikkelfiltre er eksakte; navn og alle adresser søkes med deltreff uten hensyn til store og små bokstaver. Alle oppgitte filtre kombineres. Kommunenummer, gårdsnummer og bruksnummer må treffe **samme** matrikkelenhet.

```text
GET /api/ftbid/sok?kommunenummer=0301&gaardsnummer=10
GET /api/ftbid/sok?adresse=storgata&prosjektnavn=utvidelse
```

Dubletter gir 409 med Location og ProblemDetails. Valideringsfeil gir 400 med standard ProblemDetails og norsk feilmelding. Uventede interne feil gir 500 med ProblemDetails, en generell norsk feilmelding og sporingsidentifikator. Dette forbedrer adapterens oppførsel, som også returnerte 400 for interne feil. Manglende eller ugyldig token gir 401; utilstrekkelige scopes gir 403. Oppretting begrenses til 200 forespørsler per minutt per konsument og API-instans, med 429 ved overskridelse.

## Regelkoder og ProblemDetails

Feilstrukturen følger mønsteret i `NIREG.Domain.Errors`, med norske navn under `Domain/Feil`:

- `Feilkode` er enumen med stabile regelkoder.
- `FeilType` inneholder `Feil = 0` og `Advarsel = 1`, som i NIREG.
- `FtbidException` er abstrakt og inneholder `Kode`, `FeilType`, melding og eventuell opprinnelig årsak.
- `ValideringsException` er abstrakt og samler forventede inndata- og forretningsfeil.
- `Unntak/` inneholder én konkret klasse per forventet feil, med relevant kontekst som kommunenummer eller ftbid.

Regler med samme betydning gjenbruker NIREGs kode. Ftbids egne regler har koder fra `V0047`; eksisterende NIREG-koder gis ikke en ny betydning.

| Kode | Unntak |
| --- | --- |
| V003 | ImportfilManglerEllerTomException |
| V0026 | GyldigFraEtterGyldigTilException |
| V0034 | UgyldigKommunenummerException |
| V0038 | DeserialiseringFeiletException |
| V0042 | PåkrevdVerdiManglerException |
| V0047 | TekstForLangException |
| V0048 | ForMangeVerdierException |
| V0049 | UgyldigeMatrikkelopplysningerException |
| V0050 | SøkeparameterManglerException |
| V0051 | UgyldigSøkenummerException |
| V0052 | UgyldigFtbidException |
| V0053 | ArkivreferanseIkkeFunnetException |
| V0054 | FtbidIkkeFunnetException |
| V0055 | UgyldigeReferansedataException |
| V0056 | UgyldigeSkjemametadataException |
| V0057 | UgyldigGs1PrefiksException |
| V0058 | SekvensnummerUtenforOmrådetException |
| V0059 | KonfigurasjonManglerException |
| V0060 | UgyldigMaskinportenkonfigurasjonException |
| V0061 | SisteMatrikkelKanIkkeFjernesException |
| V0062 | ProsjektidentitetAlleredeIBrukException |
| V0063 | MatrikkelenhetIkkeFunnetException |
| V0064 | MatrikkelOppslagFeiletException |
| V0065 | UgyldigMatrikkelsvarException |
| V0066 | MatrikkelAutentiseringFeiletException |
| V0067 | FtbidDublettException (dublett) |
| V0068 | FtbidDublettException (sannsynlig dublett) |

HTTP-feilhåndteringen bruker ASP.NET Core `IExceptionHandler`, `UseExceptionHandler`, `AddProblemDetails`, `IProblemDetailsService` og `ProblemDetailsFactory`. `UseStatusCodePages` gir samme standardformat for tomme feilsvar, blant annet 401, 403, 404 og 429. Ugyldig JSON og modellbinding får også et problemobjekt med regelkode. Ingen egen håndskrevet HTTP-mellomvare brukes.

Svarene har innholdstype `application/problem+json`. Standardfeltene `type`, `title`, `status`, `detail` og `instance` følger ProblemDetails-standarden. Norske regelopplysninger legges i utvidelsen `ftbidFeil`, med samme feltstruktur som NIREGs feilkontrakt:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Ugyldig forespørsel",
  "status": 400,
  "detail": "Ugyldig kommunenummer (9999).",
  "instance": "/api/ftbid",
  "ftbidFeil": {
    "Kode": "V0034",
    "Beskrivelse": "Ugyldig kommunenummer (9999).",
    "Type": 0
  },
  "sporingsidentifikator": "forespørselens sporingsidentifikator"
}
```

Forventede valideringsfeil gir 400, inkludert ukjent ftbid eller arkivreferanse ved tilknytning. En ukjent ftbid ved ordinært oppslag gir fortsatt 200 med tom liste. Konfigurasjons- og sekvensfeil er andre `FtbidException`-typer; de er ikke klientfeil og gir 500 dersom de oppstår under en HTTP-forespørsel. Oppstartsfeil stopper prosessen. Uventede feil logges, men interne detaljer og databaseopplysninger sendes ikke til klienten. De har ingen oppdiktet regelkode.

## Maskinporten

`Maskinporten:Aktivert` er satt til `true` i alle fem innstillingsfiler: felles `appsettings.json` samt `Local`, `Development`, `Test` og `Production`. Flagget kan fortsatt overstyres med `Maskinporten__Aktivert`. En endring krever omstart. Når flagget er av, er ftbid-endepunktene åpne uten token og Swagger viser ingen autentiseringskrav.

I **Local** kreves kun en ikke-tom `Authorization: Bearer <token>`-header. Tokenets JWT-format, signatur, utsteder, levetid, konsument og scopes valideres ikke. Metadata og signeringsnøkler hentes ikke. Ingen claims fra det uvaliderte tokenet brukes som identitet eller organisasjonsnummer; lokale opprettinger deler derfor rategrensen. Helseendepunktene er fortsatt offentlige.

I **Development, Test og Production** brukes full JWT- og scopevalidering som beskrevet nedenfor. Den lokale forenklingen velges utelukkende av miljønavnet `Local`, ikke av et eget flagg for tokenvalidering.

`launchSettings.json` bruker `Local`, slik at `dotnet run --project src/Ftpb.Ftbid.Api` starter lokalt. For andre miljøer må oppstartsprofilen utelates:

```sh
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Ftpb.Ftbid.Api --no-launch-profile
```

[ftbid_endepunkter_local.http](ftbid_endepunkter_local.http) inneholder eksempler på alle API-endepunkter, helse, OpenAPI og Swagger. Eksempeltokenet er en syntetisk JWT med ugyldig signatur og utløpt levetid, som bare fungerer i Local. Lim eventuelt inn et ekte Maskinporten-token i `token`-variabelen. Kjør opprettingsforespørselen først i Riders HTTP-klient; svaret lagres til senere oppslag og tilknytning. `AR123456789` forutsetter import av eksempelfilen for referansedata.

Utenfor Local sendes token i `Authorization: Bearer <tilgangstoken>`. Servicen bruker JWT Bearer og henter metadata fra utstederens `.well-known/oauth-authorization-server`. Biblioteket håndterer henting og mellomlagring av signeringsnøkler. Signatur, RSA-algoritme, utsteder og levetid kontrolleres; tidsavviket er 30 sekunder. Scopes kontrolleres som hele verdier. `consumer.ID` må inneholde et norsk organisasjonsnummer på formen `0192:123456789`.

NIREGs særskilte SKO-rollekontroll videreføres ikke, siden denne servicen autoriserer med ftbid-scopes. Publikumskontroll er som i NIREG avskrudd når `Maskinporten:Publikum` er tom; sett verdien hvis API-et bruker publikumsbegrensede token. Når Maskinporten er aktivert, har Swagger en Authorize-knapp for innliming av token.

Scopene er konfigurerbare. Tilganger må være registrert i Maskinporten for konsumentene; denne løsningen oppretter ikke scopes eller klientregistreringer hos Digdir. Se [Digdirs dokumentasjon om token og validering](https://docs.digdir.no/docs/Maskinporten/maskinporten_protocol_token.html).

## Kartverket og obligatoriske matrikkelenheter

En ftbid må ha minst én matrikkelenhet. Oppretting uten matrikkel avvises, og den siste matrikkelenheten kan ikke slettes (`V0061`). Alle matrikkelenheter i en opprettingsforespørsel og alle nye tillegg valideres før noen data skrives. Hvis én enhet ikke kan godkjennes, avvises hele opprettingen eller tilknytningen.

`IMatrikkelService` og `MatrikkelService` gjør den autoritative kontrollen. `IMatrikkelKlient` og `KartverketMatrikkelKlient` henter identifikatorer fra `MatrikkelenhetServiceWS/findMatrikkelenheter` med SOAP 1.1. Oppsettet følger Niregs `MatrikkelService`: HTTPS med Basic-autentisering, samme SOAP-navnerom og kontekst (`no_NO`, koordinatsystem 84, systemversjon `trunk` og gjeldende snapshot). Alle fem matrikkelfelter sendes som eksakte filtre; feste- og seksjonsnummer sendes også når de er 0. Ingen adresser, eiere eller personopplysninger hentes ved denne valideringen.

Standardendepunktet er `https://nd.matrikkel.no/matrikkelapi/wsapi/v1/`, som i Nireg. Det kan overstyres gjennom `Matrikkel:Basisadresse`. Bruk en Kartverket-konto med lesetilgang til dette endepunktet. Brukernavn og passord finnes ikke i prosjektets innstillingsfiler eller Docker-image.

For Compose: kopier `.env.eksempel` til `.env` og fyll inn samme Kartverket-innlogging som i Nireg:

```text
MATRIKKEL_BRUKERNAVN=<Kartverket-brukernavn>
MATRIKKEL_PASSORD=<Kartverket-passord>
```

Start API-containeren på nytt etter at `.env` er endret:

```sh
docker compose up -d --force-recreate api
```

Ved direkte .NET-kjøring eller utrulling brukes `Matrikkel__Brukernavn` og `Matrikkel__Passord`. Disse tilsvarer `StatkartMatrikkel:UserName` og `StatkartMatrikkel:Password` i Nireg. `.env` leses av Compose, ikke automatisk av `dotnet run`. Tidsgrensen styres med `Matrikkel__TidsgrenseSekunder` (standard 15, tillatt 1–120).

Valideringen gjelder i **alle miljøer, også Local**. Den lokale forenklingen av Maskinporten-token påvirker ikke autentisering eller validering mot Kartverket. Det finnes ingen innstilling for å hoppe over Matrikkel-validering.

| Situasjon | HTTP-status | Regelkode |
| --- | --- | --- |
| Gyldig søkeresultat uten treff | 400 | V0063 |
| Nettverksfeil, tidsavbrudd, HTTP-feil eller SOAP-fault | 503 | V0064 |
| Ugyldig eller uventet SOAP-svar | 502 | V0065 |
| Kartverket avviser Basic-innlogging (401/403) | 503 | V0066 |
| Manglende brukernavn/passord eller ugyldig konfigurasjon | 500 | V0059 |

Ved feil knyttes ingen nye matrikkelopplysninger til prosjektet. API-et kan fortsatt starte og tilby oppslag, helse og adresseendringer når Kartverket-innlogging mangler, men matrikkeltillegg og prosjektoppretting avvises. Innlogging, SOAP-feildetaljer og andre interne detaljer sendes ikke til API-klienten. Kartverket-kall har tidsgrense, bruker kansellering og følger ikke HTTP-omdirigeringer med Basic-innlogging.

Historiske v1-data og syntetiske verdier fra den tidligere importen er ikke automatisk revalidert eller endret. V1-importskriptet er et separat migreringsverktøy og utfører ikke Kartverket-oppslag. Det skal ikke brukes som alternativ til de validerte API-endepunktene for nye matrikkeltilknytninger. Sletting av historiske matrikler krever fortsatt ikke nytt oppslag, så feil eller utgåtte opplysninger kan ryddes opp.

Testene bruker en uttrykkelig testklient for syntetiske matrikler. SOAP-klienten testes med kontrollerte HTTP-svar, og integrasjonstestene verifiserer at tomme treff og oppslagsfeil ikke skriver til PostgreSQL. Et ekte oppslag krever konfigurert Kartverket-innlogging.

## Referansedata

Det gamle søket hentet skjematype fra `FormMetadata` og tiltakstyper fra søkeindeksen. Den nye servicen lagrer disse opplysningene i `skjemametadata`. Kommuneregisteret lagres i `kommune`. Dette holder servicen uavhengig av adapterens database og søkeindeks.

`data/referansedata.eksempel.json` viser importformatet. Tidspunkter er ISO 8601 med UTC (`Z` eller `+00:00`). Importen validerer filen og oppdaterer eksisterende kommuner/skjemaer atomisk etter nøkkel. Utelatte oppføringer slettes ikke. Tilknytninger i `identifikasjonsmetadata` bevares ved oppdatering av skjemametadata.

```sh
dotnet run --project src/Ftpb.Ftbid.Api -- --importer /sti/til/referansedata.json
```

Importkommandoen kjører migrasjoner og avslutter uten å starte HTTP-tjeneren. Eksport fra adapteren og automatisk synkronisering er ikke satt opp. V1-uttrekkene er importert til lokal utviklingsdatabase, som beskrevet nedenfor; ingen produksjonsdatabase er endret. Ved bruk av eksisterende ftbider må EAN-verdier bevares, tidsstempler konverteres til UTC, hovedverdier og barnelister fylles, og `ftbid_sekvens` flyttes forbi høyeste importerte sekvensnummer før nye identifikatorer utstedes.

## Import av v1-uttrekk

De lokale CSV-filene uten overskriftsrad er importert med `scripts/importer_v1.py`. Kolonnerekkefølgen er:

```text
Identifikatoruttrekk: EAN, System, Distributed, Kommunenummer, Gaardsnummer, Bruksnummer, Prosjektnummer, Prosjektnavn, Festenummer, Seksjonsnummer, Adresse
Koblingsuttrekk: ArchiveReference, FtbId
```

Dette er navnene i kildedatabasen. Målmodellen bruker norske navn. Importen bevarer 13 131 identifikatorer og alle 30 944 tilknytninger. Hver ftbid får en hovedmatrikkelenhet i `matrikkelopplysning`. Uttrekket har ingen adresser. `Distributed` tolkes som norsk lokal tid (`Europe/Oslo`) og konverteres til UTC.

På 30 tilfeldig utvalgte ftbid-er er det lagt til to syntetiske adresser og to ekstra matrikkelenheter per prosjekt: 60 adresser og 60 ekstra matrikkelenheter totalt. Tilfeldighetsfrøet er `20261008`, slik at samme utvalg kan gjenskapes. Adressene begynner med `[SYNTETISKE TESTDATA]`. Matrikkelenhetene bruker kunstige gårdsnummer over 9 000 000 og er også listet i rapporten. Opprinnelige hovedverdier i `ftbid` endres ikke. De syntetiske matrikkelenhetene er ikke reelle eiendommer.

Alle arkivreferansene registreres i `skjemametadata` for å støtte fremmednøkkelen. Skjematype settes til null og tiltakstyper til en tom liste, fordi disse opplysningene ikke finnes i CSV-uttrekkene. Eksisterende skjemametadata overskrives ikke. Historiske kommunenummer importeres som del av prosjektdata; importen gjør dem ikke automatisk gyldige i dagens kommuneregister.

Høyeste importerte sekvensnummer er 13 132. Sekvensen flyttes minst til dette nummeret, slik at neste nye identifikator bruker 13 133 eller høyere. GS1-prefikset i lokal utvikling er satt til `7073387`, som i uttrekket.

Importen validerer kolonner, feltgrenser, EAN-kontrollsifre, duplikater og koblinger. Skrivingen er samlet i én transaksjon og bruker samme PostgreSQL-lås som API-ets oppretting. Like rader kan importeres på nytt uten duplikater. Ved konflikter med eksisterende verdier avbrytes importen uten overskriving. En sekvens kan bare flyttes fremover. Før skriving tas en sikkerhetskopi med `pg_dump`.

```sh
# Valider og lag importfil og rapport uten databaseskriving:
python3 scripts/importer_v1.py --ftbider /sti/til/FtbIds.csv --koblinger /sti/til/FormWorkflowLogs.csv
# Importer til prosjektets lokale Compose-database:
python3 scripts/importer_v1.py --ftbider /sti/til/FtbIds.csv --koblinger /sti/til/FormWorkflowLogs.csv --utfor
```

Sikkerhetskopier, SQL-import og rapport med alle syntetiske verdier finnes lokalt i `data/import-v1/`. Mappen er ignorert av Git. Ikke bruk syntetiske verdier i produksjon. En import uten syntetiske tillegg kan genereres med `--testprosjekter 0`.

## Database og miljøer

`ftbid` bevarer opplysningene fra `Ftbids`, inkludert EAN, prosjektinformasjon, system og utstedelsestidspunkt. `identifikasjonsmetadata` bevarer relasjonen fra `FormWorkflowLogs`: arkivreferanse og ftbid. `adresse` og `matrikkelopplysning` inneholder flere verdier per ftbid. Hovedverdiene ligger også i `ftbid` for å bevare prosjektidentiteten. `skjemametadata` og `kommune` inneholder nødvendige referansedata.

| ASPNETCORE_ENVIRONMENT | Maskinporten | PostgreSQL | Migrering ved oppstart | Swagger |
| --- | --- | --- | --- | --- |
| Local | Kun tokenets tilstedeværelse | Lokal standardtilkobling | Ja | Ja |
| Development | test.maskinporten.no, full validering | Miljøvariabel | Nei | Ja |
| Test | test.maskinporten.no, full validering | Miljøvariabel | Nei | Ja |
| Production | maskinporten.no, full validering | Miljøvariabel | Nei | Nei |

Development, Test og Production krever `ConnectionStrings__Postgres`. Ingen passord for disse miljøene ligger i konfigurasjonsfilene. Eksempel på miljøvariabler i driftsplattformen:

```text
ASPNETCORE_ENVIRONMENT=Test
ConnectionStrings__Postgres=Host=<vert>;Port=5432;Database=<database>;Username=<bruker>;Password=<hemmelighet>;SSL Mode=VerifyFull
Maskinporten__LesScope=dibk:bygg/ftbid.les
Maskinporten__EndreScope=dibk:bygg/ftbid.endre
```

API-et forutsetter HTTPS ved eksponering utenfor lokal maskin, for eksempel via en TLS-terminerende reverse proxy. Docker-imaget kjører som en bruker uten administratorrettigheter på port 8080.

Kjør migrasjoner som et eget utrullingssteg i test og produksjon, med nødvendige databaserettigheter:

```sh
dotnet run --project src/Ftpb.Ftbid.Api --no-launch-profile -- --migrer
# For publisert utgave:
dotnet Ftpb.Ftbid.Api.dll --migrer
```

Opprett nye migrasjoner med det lokale EF-verktøyet:

```sh
dotnet tool restore
dotnet ef migrations add <NorskMigrasjonsnavn> --project src/Ftpb.Ftbid.Infrastructure --startup-project src/Ftpb.Ftbid.Api --output-dir Migrasjoner
```

Se [Npgsqls dokumentasjon for EF Core 10](https://www.npgsql.org/efcore/release-notes/10.0.html) og [ASP.NET Cores dokumentasjon for ratebegrensning](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0).

## Tester

```sh
docker compose up -d --wait postgres
dotnet test ftpb-ftbid-api.sln
```

Integrasjonstestene lager en egen `ftbid_test_<tilfeldig verdi>`-database, kjører migrasjoner og fjerner kun denne databasen etterpå. De bruker ikke den vanlige utviklingsdatabasen. Testbrukeren må kunne opprette og slette testdatabaser. Sett `FTBID_TEST_POSTGRES` for å bruke en annen lokal PostgreSQL-instans; standarden er Compose-instansen.

Testene dekker EAN-kontrollsiffer, oppretting, oppslag, tilknytning og eiendomsendringer, JSON-kontrakt, kort ftbid, flere adresser og matrikler, eksakte kombinerte matrikkelfiltre, samtidige opprettinger, prosjektnummer, tilknytning/flytting, validering, kommunegyldighet, ratebegrensning, helse, OpenAPI, autentisering og ProblemDetails med stabile regelkoder. RSA-signerte testtoken går gjennom samme JWT-validering som produksjon; feil signatur, utsteder, levetid, scopes og konsument kontrolleres. Nøkler og metadata er lokale i testene, slik at de ikke avhenger av tilgang til Digdir.

## Lokal navneendring

Løsningen og prosjektmappen heter `Ftpb.Ftbid` og `ftpb-ftbid-api`. API-rutene ligger under `/api/ftbid`, JSON-feltet heter `Ftbid`, og problemutvidelsen heter `ftbidFeil`. Maskinporten-konfigurasjonen bruker `dibk:bygg/ftbid.les` og `dibk:bygg/ftbid.endre`; disse verdiene må samsvare med scopene registrert hos Digdir.

Lokal PostgreSQL bruker databasen og brukeren `ftbid`, med eget Compose-volum. Alle importerte identifikatorer, koblinger, testverdier og sekvensnummer er bevart. Tabeller, kolonner, indekser og migrasjonshistorikk er oppdatert til navnene i den nye løsningen.

En oppdatert sikkerhetskopi finnes i `data/import-v1/sikkerhetskopi-ftbid.dump`. Originale kilder og sikkerhetskopier fra tidligere import er historiske arkiver og beholder opprinnelige navn internt. De inngår ikke i aktiv kode eller konfigurasjon. Den tidligere lokale databasecontaineren er stoppet og beholdt som en ekstra kopi.

## Flytting til ftpb-ftbid-api

Alle servicefiler, lokale data og importarkiver ligger nå i denne repositoryen. Løsningsfilen heter `ftpb-ftbid-api.sln`, og .NET-prosjektene heter `Ftpb.Ftbid.Api`, `Ftpb.Ftbid.Domain`, `Ftpb.Ftbid.Application`, `Ftpb.Ftbid.Infrastructure` og `Ftpb.Ftbid.Tests`.

Målrepositoryens `develop`-gren og GitHub-remote er beholdt. Den tidligere lokale Git-historikken er bevart på grenen `historikk/ftbid-provider-service`. Flyttingen er ikke automatisk committet eller pushet.

Compose-prosjektet og API-imaget heter `ftpb-ftbid-api`. PostgreSQL-dataene er kopiert fra den stoppede databasen til volumet `ftpb-ftbid-api_ftbid-postgres`. Det opprinnelige volumet er beholdt som sikkerhetskopi. Identifikatorer, tilknytninger, adresser, matrikkelopplysninger og sekvensnummer er bevart.
