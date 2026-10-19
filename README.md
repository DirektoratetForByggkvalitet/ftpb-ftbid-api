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
| Domain | Ftbid, eiendommer med identifikasjon og strukturerte adresser, identifikasjonsmetadata, EAN og regelunntak |
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
| Application | `Kontrakter`, `Services`, `Lagring`, `Validering`, `Konfigurasjon`, `Feil` |
| Infrastructure | `Persistens/Lagre`, `Referansedata`, `Migrasjoner` |
| Tests | `Enhet`, `Integrasjon`, `Oppsett` |

`FtbidOpprettingService` validerer prosjektdata og oppretter en identifikator. `FtbidDublettService` kontrollerer den samlede dublettnøkkelen før lagring, under PostgreSQL-transaksjonslåsen. `FtbidOppslagService` håndterer søk, oppslag og resultatmodeller. `FtbidTilknytningService` håndterer arkivreferanser. `FtbidEiendomService` validerer tillegg og sletting av adresser og matrikkelenheter, med atomisk lagring gjennom `IFtbidEiendomslager` og `PostgresFtbidEiendomslager`. Servicene har egne grensesnitt som kontrolleren får gjennom avhengighetsinjeksjon.

`IFtbidLager` samler PostgreSQL-operasjonene for prosjektet og dets tilknytninger. Opprettingens transaksjon, sekvenstildeling og duplikatbeskyttelse ligger samlet i `PostgresFtbidLager`. Kommuneoppslag har en egen kontrakt, `IKommuneregister`, og implementeres av `PostgresKommuneregister`. `IReferansedataImportService` brukes av importkommandoen og implementeres i Infrastructure, med atomisk oppdatering av kommuner og skjemaer.

Felles tekstvalidering og normalisering er interne hjelpefunksjoner i Application. De trenger ingen egen injisert service. Serviceregistreringen er samlet i `Api/Konfigurasjon/Serviceregistrering.cs`. API-kontrakten og databaseskjemaet er uendret av denne oppdelingen.

## Endepunkter

| Metode | Sti | Scope | Svar ved suksess |
| --- | --- | --- | --- |
| POST | `/api/ftbid` | `dibk:bygg/ftbid.endre` | 200 med ftbid som JSON-streng; 409 ved dublett |
| POST | `/api/ftbid/tilknytt` | `dibk:bygg/ftbid.endre` | 204 |
| GET | `/api/ftbid/sok` | `dibk:bygg/ftbid.les` | 200 med liste av prosjekter og metadata; 404 uten treff |
| GET | `/api/ftbid/{ftbid}` | `dibk:bygg/ftbid.les` | 200 med liste, tom ved ukjent ftbid |
| POST | `/api/ftbid/{ftbid}/eiendom` | `dibk:bygg/ftbid.endre` | 204 |
| PUT | `/api/ftbid/{ftbid}/eiendom/{eiendomId}` | `dibk:bygg/ftbid.endre` | 204 |
| DELETE | `/api/ftbid/{ftbid}/eiendom/{eiendomId}` | `dibk:bygg/ftbid.endre` | 204 |

API-ruter og JSON-feltnavn er på norsk. Tilknytning bruker `Ftbid` og `Arkivreferanse`, og svarene bruker `TilknyttedeSkjemaer` med `Arkivreferanse`, `Skjematype` og `Tiltakstyper`. JSON-inndata er ikke følsomme for store og små bokstaver. Dette er en bevisst endring fra adapterens engelske ruter og JSON-feltnavn; klienter må oppdateres til den nye kontrakten.

### Eiendomsmodell og oppretting

Ved oppretting må `Eiendom` inneholde **1 til 100 eiendommer**. Eksisterende prosjekter kan ha en tom liste, og siste eiendom kan fortsatt slettes. Hver eiendom inneholder ett `Eiendomsidentifikasjon`-objekt, en `Adresse`-liste med 0 til 100 adresseobjekter, og valgfrie `Bygningsnummer`, `Bolignummer` og `Kommunenavn`. Rekkefølgen bevares. Eiendomsopplysninger har ingen eksterne registeroppslag.

```json
{
  "Prosjektnavn": "Utvidelse av bygget",
  "Prosjektnr": "P-1001",
  "Tiltakstype": "Tilbygg",
  "System": "Søknadssystem",
  "Eiendom": [
    {
      "Eiendomsidentifikasjon": {
        "Kommunenummer": "3901",
        "Gaardsnummer": 124,
        "Bruksnummer": 55,
        "Festenummer": 2,
        "Seksjonsnummer": 0
      },
      "Adresse": [
        {
          "Adresselinje1": "Admiralveien 2A",
          "Adresselinje2": "Hyttefelt 1",
          "Adresselinje3": null,
          "Postnr": "3183",
          "Poststed": "Horten",
          "Landkode": "NO",
          "Gatenavn": "Admiralveien",
          "Husnr": "2",
          "Bokstav": "A"
        }
      ],
      "Bygningsnummer": "300827824",
      "Bolignummer": "H0201",
      "Kommunenavn": "Horten"
    }
  ]
}
```

Kommunen må finnes og være gyldig i prosjektets lokale kommuneregister ved oppretting eller endring. Eksempelet bruker Horten (`3901`); den lokale referansefilen inneholder kun Oslo (`0301`). Kommuneformat og ikke-negative matrikkelnummer kontrolleres lokalt. Feste- og seksjonsnummer er 0 når de utelates. En eiendom uten identifikasjon avvises. Adressefelter er valgfrie, men et adresseobjekt må inneholde minst én verdi; null-elementer tillates ikke. Postnummer, husnummer, bygningsnummer og bolignummer er tekst, slik at ledende nuller bevares.

`Adresselinje1`–`Adresselinje3` tillater 1000 tegn hver, `Postnr` 20, `Poststed` 200, `Landkode` 2, `Gatenavn` 500, `Husnr` 50 og `Bokstav` 20. Landkode normaliseres til store bokstaver. `Bygningsnummer` og `Bolignummer` tillater 50 tegn, `Kommunenavn` 200. Tekst trimmes og tom valgfritekst blir null.

Dette erstatter de tidligere flate `Adresser`, `Matrikkelopplysninger`, adresse-, matrikkel-, bygningsnummer- og bruksenhetsfeltene på prosjektnivå. Klienter må bruke den nye kontrakten; gamle eller ukjente felt i oppretting, eiendom og adresse avvises med 400 for å unngå stille tap av opplysninger. API-et bruker PascalCase på feltnavn, og inndata er ikke følsomme for store/små bokstaver. Oppslag og søk returnerer samme struktur; hvert eiendomselement har i tillegg en stabil `Id` for endring og sletting.

Prosjektet støtter `EksternProsjektreferanse` (200 tegn), `KommunaltSaksnummer` (200), `Tiltakshaver` (500), `Organisasjonsnummer` (9), `AnsvarligSokerOrganisasjonsnummer` (ni sifre) og `Tiltakstype` (200). `Prosjektnavn` er også tiltaksnavn; `Prosjektnr` brukes som reserve for ekstern referanse.

Dublettkontrollen bruker kun kombinasjonen av `Prosjektnavn`, `Prosjektnr` (prosjektnummer), `Tiltakstype` og matrikkelens `Kommunenummer`, `Gaardsnummer` og `Bruksnummer`. Alle tre tekstfeltene er påkrevd og kan ikke være null, tomme eller bare mellomrom. `Eiendom` må inneholde minst én eiendom med alle tre matrikkelfeltene. Gårds- og bruksnummer må oppgis som ikke-negative heltall; manglende eller null verdier avvises med 400.

Alle fire opplysninger må samsvare. Ved flere eiendommer er ett felles matrikkelnummer tilstrekkelig, og kommune, gård og bruk må treffe samme eiendom. Feste- og seksjonsnummer inngår ikke i dublettnøkkelen. Tekst sammenlignes uten hensyn til store/små bokstaver og med normaliserte mellomrom og Unicode. Andre felter og lignende navn gir ikke dublett. Ved flere treff returneres ftbid i stigende rekkefølge.

En dublett avvises med **409 Conflict** og ProblemDetails med `ftbidFeil.Kode = V0067`, en beskrivende feilmelding, `eksisterendeFtbid` og `samsvarendeFelter`. Svaret har ingen `Location`-header. Eksisterende prosjekt endres ikke. Samtidige opprettinger beskyttes av PostgreSQL-transaksjonslåsen på tvers av serviceinstanser.


EAN-formatet er sju sifre GS1-prefiks, tre reserverte nuller, sju sifre PostgreSQL-sekvens og ett kontrollsiffer. Grunnkonfigurasjonen beholder adapterens eksempelprefiks `7099999`. Lokal utvikling bruker `7073387`, som samsvarer med det importerte v1-uttrekket. Prefikset kan endres med `Ftbid__Gs1Prefiks`; bruk samme prefiks som datagrunnlaget for kortoppslag. Sekvensen kan ikke gå rundt og gjenbruke identifikatorer.

### Tilknytning og oppslag

```json
{ "Ftbid": "707338700000000014", "Arkivreferanse": "AR123456789" }
```

Arkivreferansen må finnes i `skjemametadata`, og ftbid må finnes i `ftbid`. En arkivreferanse kan være tilknyttet én ftbid om gangen. Gjentatt tilknytning er idempotent; tilknytning til en annen ftbid flytter referansen. Full ftbid og de siste åtte sifrene kan brukes til oppslag; kortformen utvides med konfigurert GS1-prefiks og tre nuller.

### Endre eiendommer

Alle endringer krever endrescope og støtter full ftbid eller kortformen. De lagres atomisk med samme transaksjonslås som oppretting.

- `POST /api/ftbid/{ftbid}/eiendom` legger til et `Eiendomsdata`-objekt, som et element i eksempelets liste. Hent prosjektet for å få den nye eiendommens `Id`.
- `PUT /api/ftbid/{ftbid}/eiendom/{eiendomId}` erstatter hele eiendommen og dens adresseliste med samme struktur. ID og ftbid bevares. Utelatte valgfrie felt tømmes.
- `DELETE /api/ftbid/{ftbid}/eiendom/{eiendomId}` sletter eiendommen og dens adresser. Også den siste eiendommen kan slettes; gjentatt sletting gir 204.

Vellykkede endringer gir 204. Ukjent ftbid gir `V0054`, og erstatning av en eiendom som ikke tilhører denne ftbid-en gir `V0071`. Sletting av en ID på en annen ftbid endrer ingen eiendom. Tillegg eller endring som kolliderer med et annet prosjekts normaliserte navn og fullstendige eiendomsidentifikasjon avvises med `V0062`. De gamle `/adresser`- og `/matrikkelopplysninger`-rutene er fjernet.

### Søk

`GET /api/ftbid/sok` krever minst ett ikke-tomt filter. Alle oppgitte filtre kombineres med **OG**. Tekst trimmes og sammenlignes uten hensyn til store og små bokstaver.

| Kriterium | Søkeparameter | Treffregel |
| --- | --- | --- |
| FtID (eksisterende Ftbid) | `ftid` | Eksakt full identifikator eller de siste åtte sifrene, utvidet med konfigurert GS1-prefiks |
| Prosjektnavn/tiltaksnavn | `prosjektnavn`, `tiltaksnavn` | Deltreff i samme prosjektnavn; begge filtre må treffe hvis begge oppgis |
| Tiltakstype | `tiltakstype` | Eksakt prosjektverdi eller tiltakstype fra et tilknyttet skjema |
| Ekstern prosjektreferanse | `eksternprosjektreferanse` | Eksakt; bruker eldre `Prosjektnr` som reserve når ekstern referanse mangler |
| Kommunalt saksnummer | `kommunaltsaksnummer` | Eksakt |
| Organisasjonsnummer til ansvarlig søker | `ansvarligsokerorganisasjonsnummer` | Eksakt, ni sifre; separat fra `Organisasjonsnummer` for tiltakshaver |
| Bygningsnummer | `bygningsnummer` | Eksakt |
| Bolignummer | `bolignummer` | Eksakt på eiendommen |
| Adresse | `adresse` | Deltreff i strukturerte adressefelter på eiendommen |
| Kommune og matrikkel | `kommunenummer`, `gaardsnummer`, `bruksnummer`, `festenummer`, `seksjonsnummer` | Eksakte verdier på **samme** eiendom |

Matrikkelnummer må være ikke-negative. Utelatt feste- eller seksjonsnummer avgrenser ikke søket; `0` søker uttrykkelig etter null. Kommunenummer alene er tillatt, må ha fire sifre og kontrolleres ikke mot dagens kommuneregister, slik at historiske data kan søkes. Ugyldige identifikatorer, organisasjonsnummer og tall gir 400 med ProblemDetails.

Oppretting og oppslag inkluderer `AnsvarligSokerOrganisasjonsnummer` (ni sifre); `Bolignummer` ligger under `Eiendom`. Verdiene må registreres ved oppretting for å kunne gi treff; det eksisterende organisasjonsnummeret kopieres ikke til ansvarlig søker. Migrationen `FjernFtIdType` fjerner den tidligere identifikatortypen fra databasen. Prosjekter har ikke et eget statusfelt; status i helsesvar og ProblemDetails beskriver tjenesten og HTTP-svaret.

```text
GET /api/ftbid/sok?kommunenummer=0301&gaardsnummer=10
GET /api/ftbid/sok?adresse=storgata&prosjektnavn=utvidelse
GET /api/ftbid/sok?ftid=707338700000000014
GET /api/ftbid/sok?ansvarligsokerorganisasjonsnummer=123456789&bolignummer=H0101
GET /api/ftbid/sok?kommunenummer=0301&gaardsnummer=10&bruksnummer=1&festenummer=0&seksjonsnummer=2
```

Dubletter gir 409 med eksisterende ftbid i ProblemDetails, uten Location-header. Søk uten treff gir 404 med kode `V0072`. Valideringsfeil gir 400 med standard ProblemDetails og norsk feilmelding. Uventede interne feil gir 500 med ProblemDetails, en generell norsk feilmelding og sporingsidentifikator. Dette forbedrer adapterens oppførsel, som også returnerte 400 for interne feil. Manglende eller ugyldig token gir 401; utilstrekkelige scopes gir 403. Oppretting begrenses til 200 forespørsler per minutt per konsument og API-instans, med 429 ved overskridelse.

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
| V0062 | ProsjektidentitetAlleredeIBrukException |
| V0067 | FtbidDublettException (dublett) |
| V0068 | Utgått (tidligere sannsynlig dublett) |
| V0070 | UgyldigOrganisasjonsnummerException |
| V0071 | EiendomIkkeFunnetException |
| V0072 | SøkeresultatIkkeFunnetException |

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

## Migrering til eiendomsmodellen

Migrationen `InnforEiendomsmodell` oppretter `eiendom` og `eiendomsadresse`. Den bevarer EAN, prosjektdata og skjematilknytninger, og konverterer eksisterende data slik:

- Én eiendom per tidligere matrikkelenhet, med samme rekkefølge.
- Alle gamle adresser legges på første eiendom som `Adresselinje1`; adresser kopieres ikke til andre eiendommer.
- Prosjektets tidligere bygningsnummer og bruksenhetsnummer legges på første eiendom som `Bygningsnummer` og `Bolignummer`.
- Det fylles ikke inn oppdiktede postnummer, kommunenavn eller andre strukturerte adressefelt.

Migrationen kopierer de gamle verdiene til midlertidige tabeller før gamle tabeller og kolonner fjernes. Den nye modellen tillater null eiendommer. Automatisk `Down` støttes ikke, fordi den gamle modellen ikke kan representere eiendomsløse prosjekter eller alle de strukturerte adressefeltene; bruk en sikkerhetskopi ved tilbakeføring. Migrationen er ikke automatisk anvendt av kodeendringen, men Local kjører som før migrasjoner ved neste oppstart.

Det gjøres ingen oppslag mot Kartverkets Matrikkel. Lokal kommune- og formatvalidering beholdes. Regelkodene V0061, V0063–V0066 og V0069 er utgått og gjenbrukes ikke.

## Referansedata

Det gamle søket hentet skjematype fra `FormMetadata` og tiltakstyper fra søkeindeksen. Den nye servicen lagrer disse opplysningene i `skjemametadata`. Kommuneregisteret lagres i `kommune`. Dette holder servicen uavhengig av adapterens database og søkeindeks.

`data/referansedata.eksempel.json` viser importformatet. Tidspunkter er ISO 8601 med UTC (`Z` eller `+00:00`). Importen validerer filen og oppdaterer eksisterende kommuner/skjemaer atomisk etter nøkkel. Utelatte oppføringer slettes ikke. Tilknytninger i `identifikasjonsmetadata` bevares ved oppdatering av skjemametadata.

```sh
dotnet run --project src/Ftpb.Ftbid.Api -- --importer /sti/til/referansedata.json
```

Importkommandoen kjører migrasjoner og avslutter uten å starte HTTP-tjeneren. Eksport fra adapteren og automatisk synkronisering er ikke satt opp. V1-uttrekkene er importert til lokal utviklingsdatabase, som beskrevet nedenfor; ingen produksjonsdatabase er endret. Ved bruk av eksisterende ftbider må EAN-verdier bevares, tidsstempler konverteres til UTC, eiendommer og adresselister fylles, og `ftbid_sekvens` flyttes forbi høyeste importerte sekvensnummer før nye identifikatorer utstedes.

## Import av v1-uttrekk

De lokale CSV-filene uten overskriftsrad er importert med `scripts/importer_v1.py`. Kolonnerekkefølgen er:

```text
Identifikatoruttrekk: EAN, System, Distributed, Kommunenummer, Gaardsnummer, Bruksnummer, Prosjektnummer, Prosjektnavn, Festenummer, Seksjonsnummer, Adresse
Koblingsuttrekk: ArchiveReference, FtbId
```

Dette er navnene i kildedatabasen. Målmodellen bruker norske navn. Importen bevarer 13 131 identifikatorer og alle 30 944 tilknytninger. Hver ftbid får en eiendom i `eiendom`. Uttrekket har ingen adresser. `Distributed` tolkes som norsk lokal tid (`Europe/Oslo`) og konverteres til UTC.

På 30 tilfeldig utvalgte ftbid-er er det lagt til to syntetiske adresser og to ekstra matrikkelenheter per prosjekt: 60 adresser og 60 ekstra matrikkelenheter totalt. Tilfeldighetsfrøet er `20261008`, slik at samme utvalg kan gjenskapes. Adressene begynner med `[SYNTETISKE TESTDATA]`. Matrikkelenhetene bruker kunstige gårdsnummer over 9 000 000 og er også listet i rapporten. Opprinnelig eiendomsidentifikasjon bevares. De syntetiske matrikkelenhetene er ikke reelle eiendommer.

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

`ftbid` bevarer opplysningene fra `Ftbids`, inkludert EAN, prosjektinformasjon, system og utstedelsestidspunkt. `identifikasjonsmetadata` bevarer relasjonen fra `FormWorkflowLogs`: arkivreferanse og ftbid. `eiendom` inneholder identifikasjon, bygningsnummer, bolignummer og kommunenavn per eiendom; `eiendomsadresse` inneholder de strukturerte adressene. En ftbid kan ha null eiendommer. `skjemametadata` og `kommune` inneholder nødvendige referansedata.

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

### Azure PostgreSQL med Entra ID

Passordløs databaseautentisering støttes med [Microsoft.Azure.PostgreSQL.Auth](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/microsoft.azure.postgresql.auth-readme?view=azure-dotnet) og `DefaultAzureCredential`. Sett følgende i kjøremiljøet:

```text
Database__BrukEntraId=true
ConnectionStrings__Postgres=Host=<server>.postgres.database.azure.com;Port=5432;Database=<database>;SSL Mode=VerifyFull
```

Tilkoblingen skal ikke inneholde `Password` eller `Passfile`. Biblioteket bestemmer brukernavn fra identitetens token og leverer nye token ved tilkobling. `SSL Mode=VerifyFull` anbefales; `Require` og `VerifyCA` godtas også. Npgsql forvalter en delt datakilde og tilkoblingspool, slik at autentiseringen ikke konfigureres på nytt for hver HTTP-forespørsel.

I Azure kan applikasjonen bruke systemtildelt managed identity. For en brukertildelt managed identity settes også `AZURE_CLIENT_ID` til identitetens klient-ID. Workload identity eller service principal kan brukes gjennom Azure Identitys standardmiljøvariabler. Se [Azure Identity](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme) for de ulike legitimasjonstypene.

PostgreSQL-serveren må ha Entra ID-autentisering og en Entra-administrator konfigurert. Applikasjonens identitet må være opprettet som databasebruker med nødvendige rettigheter. Migrasjonsidentiteten trenger dessuten rettigheter til å endre databaseskjemaet. Azure-ressurser, databasebrukere og tillatelser opprettes ikke av dette prosjektet.

Ved direkte lokal kjøring mot Azure kan utvikleren logge inn med `az login` før `dotnet run`. Rider bruker miljøvariablene i kjørekonfigurasjonen. En lokal Docker-container arver ikke vertsmaskinens Azure CLI-innlogging; den må få en tilgjengelig legitimasjon i sitt eget miljø. Prosjektets vanlige Compose-oppsett fortsetter å bruke lokal PostgreSQL med passord og `Database:BrukEntraId=false` som standard.

API-et, `--migrer` og `--importer` bruker samme oppsett. Også `dotnet ef` støtter Entra ID gjennom miljøvariablene `Database__BrukEntraId` og `ConnectionStrings__Postgres`; design-time-fabrikken leser ikke API-ets appsettings-filer. Eksempel i et miljø der legitimasjon er tilgjengelig:

```sh
export Database__BrukEntraId=true
export ConnectionStrings__Postgres='Host=<server>.postgres.database.azure.com;Database=<database>;SSL Mode=VerifyFull'
dotnet ef database update --project src/Ftpb.Ftbid.Infrastructure --startup-project src/Ftpb.Ftbid.Api
```

Ingen migrasjon av databaseskjemaet er nødvendig for å bytte autentiseringsmetode. Enhetstestene bruker syntetiske token og kontrollerer konfigurasjonen uten Azure-tilgang; et ekte Entra ID-databaseinnloggingsforsøk må verifiseres i Azure-miljøet.

Opprett nye migrasjoner med det lokale EF-verktøyet:

```sh
dotnet tool restore
dotnet ef migrations add <NorskMigrasjonsnavn> --project src/Ftpb.Ftbid.Infrastructure --startup-project src/Ftpb.Ftbid.Api --output-dir Migrasjoner
```

Se [Npgsqls dokumentasjon for EF Core 10](https://www.npgsql.org/efcore/release-notes/10.0.html) og [ASP.NET Cores dokumentasjon for ratebegrensning](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0).

## Tester

Kjør bare enhetstester, uten Docker eller database:

```sh
dotnet test ftpb-ftbid-api.sln --filter 'FullyQualifiedName~Ftpb.Ftbid.Tests.Enhet'
```

Hele testsuiten inkluderer HTTP-integrasjonstester med PostgreSQL:

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
