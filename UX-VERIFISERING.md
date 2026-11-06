# Brukerflyt og verifisering

Endringen bygger på main og beholder rollefordelingen: leverandør eier ressurser; offentlig bruker håndterer egne behov; administrator kan håndtere begge.

## Designvalg

| Problem | Konkret valg | Kontroll |
|---|---|---|
| For mange ressurser samtidig | SQL-paginering med 10 rader, søk og filtre; detaljer åpnes separat; kart skjules bak en utvidbar seksjon. | Test med 10 000 ressurser og 5 000 behov; sidestørrelse og HTML-mengde kontrolleres. Dette er en datamengdetest, ikke en måling av samtidige brukere. |
| Uklare treff | Samme kategori, dekkende tidsrom og enten region eller valgt kartområde er obligatorisk. Treff sorteres etter luftlinje fra behovspunktet eller områdets midtpunkt, ellers navn/id. | MariaDB-test av rekkefølge og tidsgrenser; fire kriterier vises på hvert treff. |
| Feil ressurs blir valgt | En bekreftelse viser behov, tidspunkt, ressurs og kontakt før POST. | Gammel versjon gir konflikt; ingen GET endrer tildelingen. |
| Uklart resultat | Bekreftelse, status, varsel og tildelingshistorikk lagres. | Varsler og historikk lagres i samme transaksjon som tildelingen. Tapende samtidige kall oppretter ingen beskjed. |
| Vanskelig feilretting | Navngitte felt, obligatoriske felt merket *, lenket feiloppsummering og synlig fokus. | Serverfeil testes via HTTP; klientfeil og mobilmeny kontrolleres i nettleser. |
| Kart virker ikke | Lokalt Leaflet-bibliotek, manuelle koordinater og listevisning uavhengig av kartet. | JavaScript-testene simulerer manglende bibliotek og flisfeil. Nettlesertesten bruker fungerende nettverk. |

Prinsippene følger [Nielsen Norman Groups brukbarhetsheuristikker](https://www.nngroup.com/articles/ten-usability-heuristics/), [gjenkjenning fremfor hukommelse](https://www.nngroup.com/articles/recognition-and-recall/) og W3Cs veiledning om [skjemaer](https://www.w3.org/WAI/tutorials/forms/) og [feil/tilbakemeldinger](https://www.w3.org/WAI/tutorials/forms/notifications/). Dette er konkrete designbegrunnelser, ikke en erklæring om full WCAG-samsvar.

## Viktige avgrensninger

- Avstand er luftlinje, ikke kjørerute eller reisetid. Uten behovskoordinater gis ingen avstandsbasert rangering.
- Felles kategoriforslag hjelper registrering; egne kategorier er tillatt. Systemet forstår ikke at to forskjellige ord betyr det samme.
- Ressursens tidsrom testes mot ett behovstidspunkt. Varighet, mengde og kapasitet må avtales med leverandøren.
- Tilgjengelighet er eierens opplysning. Systemet bekrefter ikke fysisk tilstedeværelse eller at eieren faktisk har kapasitet.
- Varsler er bare i portalen. Antall uleste oppdateres hvert 20. sekund når fanen er synlig. Et lagret varsel betyr ikke at mottakeren har lest det.
- Utskrift viser maksimalt 500 rader og advarer hvis den avkortes. CSV-eksport strømmer hele regionlisten med samme rolletilgang.
- Tildelingshistorikken lagres i databasen; detaljsiden viser de siste 30 hendelsene. Feilede forsøk skal ikke fremstå som gjennomførte handlinger.
- Det er ingen SMS-leverandør, e-postutsending eller selvbetjent passordgjenoppretting i denne leveransen.

## Før bruk med eksterne personer

Gjennomfør en observert test på faktiske telefoner med minst én leverandør og én offentlig aktør. La dem registrere, rette feil, finne treff, tildele, finne varselet og frigi uten veiledning. Skriv dato, enhet, tid, stoppunkter og faktisk resultat. En automatisert nettleserkontroll erstatter ikke dette. Avklar også kategorier, ressurstype/kapasitet, tidspunkter og hvem som skal følge opp varsler med de første brukerne.

Se [DRIFT.md](DRIFT.md) for skillet mellom det lokale miljøet og en ekstern drift.

## Utført kontroll 5. november 2026

- Release-build av hele løsningen: 0 feil og 0 advarsler.
- 56 av 56 .NET-tester bestått mot en isolert MariaDB, ingen hoppet over; 4 av 4 JavaScript-tester bestått.
- EF rapporterte ingen modellendringer uten migrasjon. Oppgradering fra tidligere skjema bevarer eldre ressursdata.
- Docker-web og MariaDB startet med helsesjekker. Varsler og tildeling var beholdt etter ombygging/omstart.
- Nettleser med syntetiske kontoer: matching → bekreftelse → tildeling → leverandørvarsel/kontakt → marker lest → frigivelse → status/historikk.
- Registrering med ugyldig og deretter gyldig navn, maksimal navnelengde, manuelle koordinater og tidsrom; verdier beholdt ved feil. Klientfeil flytter fokus til oppsummeringen, og feillenke flytter fokus til feltet.
- Ved 320 CSS-piksler hadde ressursliste/skjema ingen horisontal side-overflow, heller ikke med et 200 tegn langt ord. Skrivebordsvisning kontrollert ved 1280 piksler. Ingen konsollfeil observert i den kontrollerte nettleserøkten.
- Uavhengig personstudie, skjermleserrevisjon, reell nettverks-/lasttest og ekstern produksjonsdeploy er ikke gjennomført av disse kontrollene.

Oppfølging av forsiden: mørk teal/grønn hovedseksjon, gul behovsknapp og tre fargede innganger. Utloggede brukere ser «Registrer behov» både på forsiden og i menyen. Knappen går via innlogging til behovsskjemaet; offentlig rolle kreves fortsatt. Egen HTTP-test og nettleserkontroll bekrefter retur til skjemaet. Forside kontrollert ved320/1280piksler uten horisontal side-overflow.

## Områdevalg og vurdering av tilbakemelding på forsiden

Brukeren bekreftet behov for områdevalg både på behov og i ressurssøket. Det er implementert som et rektangel med to klikk/trykk og manuelle koordinater. Det krever ikke at brukeren holder og drar. Dette følger retningen i W3Cs veiledning om [alternativer til dragging](https://www.w3.org/WAI/WCAG22/Understanding/dragging-movements.html). Feltene har synlige etiketter og tilknyttede instruksjoner, jf. [W3Cs skjemaveiledning](https://www.w3.org/WAI/tutorials/forms/instructions/). Dette er ikke en erklæring om full samsvar.

Det innlimte LLM-notatet ble behandlet som en heuristisk vurdering av et skjermbilde, ikke som en brukertest eller nye oppgavekrav. Følgende forslag er tatt inn: mer konkret forklaring av formål/ressurseksempler, konsekvente handlingsnavn, tydeligere konto-/rolleinformasjon, større hjelpetekst, praktiske steg i illustrasjonen og toppnavigasjon for utloggede brukere. Innlogget arbeidsflate beholder sidemenyen. Fargepaletten beholdes. Kortene har vanlige tydelige lenker; hele kortflaten gjøres ikke til en skjult klikkflate.

Områdegrensene valideres på serveren og brukes i SQL både ved treffliste og tildeling. Ugyldige søk viser ikke et ufiltrert datasett; filtervalg og rettigheter beholdes ved feilretting. Eksisterende punkt og område kan ikke lagres samtidig. Manuelle felt er synlige uten JavaScript, også ved slik konflikt. En uferdig tegning lagres ikke, og avbryt beholder tidligere valg.

Nye tester dekker områdegrenser/validering, kantpunkter, tildeling utenfor området, kryssing av regioner, eierskap og paginering/filterbevaring. Komponenttester med falskt kart dekker to hjørner, punktbytte, avbrudd, tom markering og bibliotek-/flisfeil. Disse erstatter ikke en ny visuell nettleserkontroll av områdevelgeren. Nettlesertilgangen ble deaktivert i Work under denne oppfølgingen; tidligere kontroll av den eldre forsiden gjelder ikke automatisk den nye toppmenyen eller områdeinteraksjonen.

Sluttkontroll for områdeoppfølgingen: 73 av73 .NET-tester mot isolert MariaDB og10 av10 JavaScript-tester bestått; Release0feil/0advarsler, EF uten manglende migrasjon, Docker web/MariaDB healthy. Områdereview av kode fant tre feil (tap av filtre ved ugyldig søk, skjulte punktfelt ved punkt/område-konflikt uten JavaScript og feil store/små bokstaver i feillenker); disse er rettet, og filter-/feillenkeoppførselen har egen HTTP-test. Ny visuell nettleserkontroll gjenstår som beskrevet ovenfor.

### Oppfølging: dra for å markere

Brukeren foretrakk dragging fremfor to klikk som hovedmåte. Velg «Dra for å markere område», trykk og hold, dra ut rektangelet og slipp. Et stiplet utkast vises underveis; tidligere lagrede grenser endres først ved gyldig slipp. Kartflytting/pinch/zoom pauses under tegningen og gjenopprettes til forrige tilstand ved fullføring eller avbrudd. Avbrutt, tapt pointer eller for liten markering overskriver ikke det gamle området. «Velg to hjørner» og koordinater beholdes som alternativer. Komponenttestene er nå13 totalt (9område +4posisjon), alle bestått. Dette dokumenterer logikken uten faktisk nettleser.

Kontoflyt er et designvalg: IS-20X Case2026 side3 krever registrering/innlogging, rolle-/tilgangsskille og at innloggede brukere kan registrere behov/ressurser. Den sier ikke at bare leverandører kan opprette konto. Dagens admin-opprettede offentlige kontoer er beholdt inntil en eventuell ny registreringsflyt bestemmes. Selvregistrering med godkjenning av offentlig rolle er et mulig neste steg; ingen slik godkjenning eller selvregistrering for offentlig rolle er implementert her.

## Operativ oversikt, lister og aktivitet – 5. november 2026

Tilbakemeldingen fra skjermbildene er vurdert som forslag, ikke bevis på feil eller brukerundersøkelse. Vi tok inn tydelig prioritering av udekkede hastebehov, eksplisitt mine/alle-scope, ID og sted, direkte matchinginngang, større tekst og en bredere arbeidsflate. Varsler er kompakte og kan filtreres på lest/ulest; lest aktivitet på dashbordet er sammenfoldet. Kontaktopplysninger åpnes via detaljene. Ressursrader viser faktiske beskrivelser, uten å finne på kapasitet.

Eksisterende matching og statuser beholdes. Ny/Vurderes er åpne, Tildelt har ressurs og Løst er avsluttet. Prioritet er et eget felt. Ulest betyr ikke handling påkrevd; det er ikke innført en slik automatisk vurdering. Tilgjengelighet er eierens opplysning og må bekreftes praktisk.

Kontroll etter denne endringen: 77/77 .NET-tester mot isolert MariaDB og 13/13 JavaScript-tester bestått, ingen hoppet over. Testene omfatter mine/alle/admin-tall, sortering, oppfølgingsfiltre og paginering, eierrettigheter, private varselfiltre/lesthandlinger, eldre kontakttrailere og HTML-escaping av beskrivelsesutdrag. Release-build hadde 0 feil/0 advarsler; EF rapporterte ingen manglende migrasjon. De tidligere nettleserkontrollene over gjelder eldre skjermbilder. Ny visuell kontroll av dashbord, liste og varselvisning er ikke gjennomført fordi lokal nettlesertilgang fortsatt er deaktivert i Work. Ingen ny personstudie er gjennomført.
