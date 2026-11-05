# Ressursportalen - IS-202 Gruppe 17

ASP.NET Core MVC-applikasjon for offentlige behov og tilbudte ressurser ved kriser. Oppgave 2 bygger videre på registrering med Leaflet og legger til MariaDB, Identity, roller, ressurs-CRUD, behov, matching og tildeling.

## Oppstart med Docker

Krever Git og Docker Desktop med Linux-containere. .NET 10 SDK trengs bare når du bygger eller tester utenfor Docker. Node.js 22+ brukes til JavaScript-testene.

```powershell
git clone https://github.com/MattiSouls/IS-202-Group-17.git
cd IS-202-Group-17
Copy-Item .env.example .env
```

På macOS/Linux: bruk `cp .env.example .env`. Rediger `.env` før oppstart:

- Sett egne lange `MARIADB_PASSWORD` og `MARIADB_ROOT_PASSWORD`.
- Standardport er `WEB_PORT=8080`; databaseport er `DB_PORT=3307` på localhost.
- Fyll valgfritt ut `SEED_PUBLIC_EMAIL/PASSWORD` for en offentlig demobruker, og `SEED_ADMIN_EMAIL/PASSWORD` for administrator. Ikke bruk ekte persondata eller del passordene i Git.
- Vanlig brukerregistrering gir alltid rollen **ResourceProvider**. Offentlig rolle/admin kan ikke velges i registreringsskjemaet. Dette er kontoflyten vi har valgt; casebeskrivelsen krever rollefordeling, men sier ikke at bare leverandører kan opprette konto selv.

```powershell
docker compose up -d --build --wait
docker compose ps
```

Åpne http://localhost:8080. Begge tjenestene, `web` og `mariadb`, skal kjøre. Web starter etter database-helsesjekken og kjører EF-migrasjonene automatisk. Et enkelt `docker run` av bare web-imaget er ikke tilstrekkelig, fordi løsningen trenger databasekobling.

```powershell
docker compose logs --tail 100 web mariadb
docker compose stop
docker compose start --wait
```

MariaDB-data og ASP.NET Data Protection-nøkler ligger i navngitte Docker-volumer og beholdes ved stopp/start og vanlig `docker compose down`. Ikke bruk `down -v` på gruppas arbeidsmiljø: det sletter volumene. Ta databasebackup før endring av et miljø med viktige data. Hvis porten er opptatt, endre porten i `.env`.

Ved oppdatering: hent siste kode og kjør `docker compose up -d --build --wait`. Migrasjonen `AddResourceAvailabilityPeriod` legger til nullable tidsfelt uten å finne på datoer for eksisterende ressurser. Eieren må redigere slike ressurser og fylle ut tidsrom før de vises som offentlige tilbud eller matcher behov. Aktive tildelinger beholdes.

### Kjøring med .NET lokalt

Start databasen med `docker compose up -d mariadb`. Sett `ConnectionStrings:DefaultConnection` med `dotnet user-secrets set --project WebApplicationInAspire ...`, eller miljøvariabelen `ConnectionStrings__DefaultConnection`, til din lokale MariaDB. Bruk localhost og `DB_PORT`, ikke Compose-navnet `mariadb`.

```powershell
dotnet tool restore
dotnet ef database update --project WebApplicationInAspire
dotnet run --project WebApplicationInAspire
```

Lokale standardadresser fra launchSettings er http://localhost:5011 og https://localhost:7018. Aspire AppHost kan startes med `dotnet run --project WebApplicationInAspire.AppHost`, men AppHost oppretter ikke databasen; samme eksterne databasekonfigurasjon må være tilgjengelig. Docker Compose er den kontrollerte hovedflyten.

## Roller og bruk

| Rolle | Tilgang |
|---|---|
| ResourceProvider | Registrere, se, redigere og slette egne ressurser. Se egne tilgjengelige, utilgjengelige og tildelte ressurser. |
| PublicUser | Registrere behov, se behov og tilgjengelige ressurser, matche og håndtere egne behov. |
| Admin | Administrere ressurser og behov, også opprettet av andre. |

Alle ressurs-/behovsruter krever innlogging. Brukertilknytning settes på serveren. En annen offentlig bruker kan se et behov og mulige treff, men bare oppretter/admin kan tildele, frigi eller endre statusen.

Registrer ressurs med navn, type, region, tidsrom, tilgjengelighet, kontaktinformasjon og posisjon. Posisjon kan velges i Leaflet eller fylles inn som kjente bredde-/lengdegrader. Ved kartfeil vises en melding om koordinatfeltene; ingen posisjon gjettes. Koordinater kontrolleres på serveren også uten JavaScript. Ressursoversikten fungerer som liste selv om kartet ikke lastes.

Tidspunkter for både behov og ressurs oppgis som **norsk lokal tid**. Fra/til må være utfylt, og slutt må være etter start. Valg av tidssone og entydig håndtering av tvetydige klokkeslett ved sommertid er ikke implementert; se driftsavgrensningene i DRIFT.md. `CreatedAtUtc` er bare teknisk opprettelsestid.

### Matching og status

Treff krever samme type og enten samme region eller registrert ressursplassering innenfor behovets valgte kartområde (store/små bokstaver og ytre mellomrom ignoreres i tekstfeltene), `IsAvailable=true`, ingen aktiv tildeling, og at `NeededAt` ligger innenfor ressursens tidsrom, inkludert endepunktene. Når behovet har et punkt eller kartområde, sorteres treffene etter luftlinje fra punktet eller områdets midtpunkt i databasen før siden hentes. Uten punkt eller område brukes navn/id; avstand er ikke reisetid. Tildeling kontrollerer vilkårene på nytt på serveren; en gammel treffliste gir ikke rett til å ta en ressurs som ikke lenger er ledig.

- Ny (`New`) eller Vurderes (`UnderReview`) kan tildeles en passende ressurs.
- Tildeling setter behovet til Tildelt (`Assigned`) og ressursen utilgjengelig i én MariaDB-transaksjon.
- Frigi setter behovet til Vurderes, fjerner referansen og gjør ressursen tilgjengelig igjen.
- Løs et tildelt behov setter status Løst (`Resolved`) og frigir ressursen samlet.
- En tildelt ressurs kan ikke redigeres eller slettes før den er frigitt. Ved samtidighetskonflikt vises HTTP 409 og brukeren må oppdatere oversikten.

Ressurser og behov vises med søk, filtre og 10 rader per side. Oversikten skiller mellom tilgjengelig nå, senere, utløpt tidsrom, pauset og tildelt. Velg en region før utskrift eller CSV-eksport. Utskriften advarer når den avkortes til 500 rader; CSV strømmer hele regionlisten og nøytraliserer regnearkformler. Den følger samme rolletilgang som oversikten. Tilgjengelighet er eierens opplysning og bekrefter ikke at ressursen faktisk finnes på stedet; kontakt eieren ved bruk.

### Punkt og kartområde

På behovsskjemaet kan du velge ett punkt eller et rektangel. Åpne kartvalget, velg **Dra for å markere område**, trykk og hold i kartet, dra ut feltet og slipp for å fullføre. Kartflytting pauses mens du tegner og kommer tilbake etterpå. Alternativet **Velg to hjørner** og manuelle felt gir samme funksjon uten dragging. **Avbryt markering** beholder forrige valg; **Fjern kartvalg** fjerner avgrensningen. En uferdig tegning kan ikke sendes. Koordinatfeltene gir samme funksjon uten mus eller karttjeneste. Fire gyldige grenser kreves for et område; punkt og område kan ikke lagres samtidig.

Med et behovsområde matcher systemet ressurser med registrert punkt innenfor grensene, inkludert kanten. Området erstatter fylkes-/regionmatchingen og kan krysse regiongrenser. Ressurskategori, tidsrom, ledighet og rettigheter kontrolleres som før, også i den atomiske tildelingen. Luftlinje måles fra områdets midtpunkt, ikke fra nærmeste grense. Dette er ikke et leveringsområde eller et løfte om at ressursen kan brukes overalt i rektangelet.

I ressursoversikten kan du avgrense søket på samme måte. Bruk **Søk med kartvalget** etter markering. Områdegrensene beholdes ved sidebytte og retur fra detaljer. Ved ugyldig område vises feil og ingen resultater; andre filtre beholdes. Fjern kartvalget for å bruke regionfilteret igjen. Områdevalg er et rektangel, ikke en vilkårlig polygon eller kommunegrense. Kryssing av datolinjen støttes ikke. Regional utskrift/CSV er skjult mens et kartområde er aktivt, slik at en hel region ikke forveksles med det valgte området.

### Arbeidsflate, varsler og feilvern

Oversikten prioriterer udekkede behov med høyeste prioritet først. Offentlig bruker starter på **Mine behov**, administrator på **Alle behov**; valget er synlig og følger lenkene til oppfølgingsfiltrene. Udekket betyr åpent behov uten tildelt ressurs. «Haster» er brukerens registrerte prioritet, og «tidspunkt passert» betyr at det oppgitte behovstidspunktet har passert. Ingen av delene er en automatisk vurdering av krisens alvor. Ressursleverandøren ser egne tilbud og tildelinger. Oversikten er et tidsstemplet øyeblikksbilde; bare ulest-telleren oppdateres automatisk.

Behovslisten viser ID og tydelig prioritet, med direkte matching for oppretter/admin. Ressursoversikten gir inngang til matching fra et behov og viser utdrag av eierens beskrivelse. Kapasitet tolkes ikke fra fritekst. Tilgjengelighet er eierens opplysning, ikke en fysisk bekreftelse.

Varsler og aktivitet har filtre for alle/uleste/leste og en kompakt hendelsesliste. Oversikten fremhever egne uleste varsler; lest aktivitet kan foldes ut. Ulest er ikke det samme som at handling kreves. Nye varsler gjentar ikke kontaktopplysninger; disse finnes på de beskyttede detaljsidene. Eldre kontaktkopier er beholdt i databasen, men kontakttraileren skjules i varselvisningen.

Tildeling/frigivelse gir varig varsel til ressurseieren og behovets oppretter, og en historikkhendelse i samme transaksjon. Varslene finnes under **Varsler** og på oversikten; ingen SMS eller e-post sendes. Ulest-telleren oppdateres hvert 20. sekund når fanen er synlig. Leverandøren ser kontakt for den aktive tildelingen på ressursens detaljside.

Bekreftelsessider viser konsekvensen før tildeling/frigivelse. Versjonskontroll avviser gamle skjemaer og bekreftelser, også når et behov endres samtidig med tildeling. Registreringsnøkler hindrer at samme skjema lagres dobbelt. Konflikter gir en forklaring og vei tilbake; inntastede redigeringsverdier beholdes. Åpne behov kan redigeres og settes til vurdering/løst uten tildeling.

Felles kategoriforslag, synlige statusord, tastaturfokus, lenkede feilmeldinger og mobilmeny støtter navigasjonen. Leaflet leveres lokalt; bare kartflisene hentes eksternt. Se [UX-VERIFISERING.md](UX-VERIFISERING.md) for designbegrunnelser og funksjonens konkrete avgrensninger, og [DRIFT.md](DRIFT.md) før bruk utenfor det lokale miljøet.

## Arkitektur og kode

.NET 10 / ASP.NET Core MVC, Razor, Bootstrap, Leaflet 1.9.4/OpenStreetMap, EF Core 9/Pomelo og MariaDB 11.4.

```text
Nettleser (skjema, kart og koordinater)
  -> MVC-controller (GET/POST, validering, rolle/eierskap)
  -> Repository / AllocationService
  -> ApplicationDbContext (Identity + Resources + Needs + Notifications + AllocationEvents)
  -> MariaDB i Docker
  -> redirect til ny GET / Razor-visning
```

- `Controllers/AccountController.cs`: registrering, innlogging, utlogging og trygg lokal returadresse.
- `Controllers/ResourceController.cs`: CRUD, rolletilpasset oversikt og regional utskrift.
- `Controllers/NeedController.cs`: behov, status og tildelingshandlinger.
- `Models/*ViewModel.cs`: validerte skjemaer. `Models/Entities/` er databasemodellene.
- `DataAccess/`: DbContext, repositories og versjonerte EF-migrasjoner.
- `Services/AllocationService.cs`: matching og atomisk assign/release med betingede SQL-oppdateringer.
- `Views/Shared/_Resource*.cshtml` og `wwwroot/js/resource-position.js`: delte tids-/posisjonsfelt og kartfallback.
- `WebApplicationInAspire.Tests/`: xUnit/NSubstitute, HTTP-/MariaDB-integrasjonstester og JavaScript-tester.

Tildeling låser/oppdaterer ressursen før behovet. Bare én forespørsel kan ta ressursen, og en tapende tildeling rulles tilbake. Redigering/sletting bruker samme låserekkefølge og kontrollerer aktiv tildeling. Testene bruker ekte MariaDB fordi EF InMemory ikke verifiserer transaksjoner, SQL eller låsing.

### Kravsporbarhet for Oppgave 2

| Oppgavekrav | Implementasjon | Kontroll |
|---|---|---|
| MVC, GET/POST, skjema og kartpunkt | ResourceController, ResourceViewModel, Razor, resource-position.js | Resource_CRUD, valideringstester, nettleserregistrering og JavaScript-testene |
| App og MariaDB i Docker, varig lagring | compose.yaml, ApplicationDbContext og Migrations | Docker-oppstart, Data_survives_new_application_host og Upgrade_from_previous_release |
| Registrering og databaselagret bruker | AccountController og Identity | Registration_stores_hashed_password |
| Autentisering, autorisasjon og ressurs-CRUD | Account/ResourceController, rollenavn og repositories | Roles_ownership_CSRF, Login_rejects_external_return_url, Resource_CRUD |
| Offentlige behov, status, matching og regional liste | NeedController, AllocationService, RegionalList | Need_HTTP_flow, Matching_honors_period, samtidighetstestene og Public_and_regional_overview |
| Ressursens tidspunkt/frister | AvailableFrom/AvailableUntil i modell/skjema/database/matching | ValidationTests, Invalid_period og Matching_honors_period |
| Mobiltilpasset oversikt og skjema | Bootstrap-grid/navigasjon og responsive kart | Nettleserkontroll av smal mobilvisning; reell person-/mobiltest gjenstår |
| Enhets-, system-, sikkerhets- og brukervennlighetstesting | Testprosjekt, CI og testprotokollen nedenfor | 77 .NET + 13 JavaScript bestått lokalt; personstudien er ikke utført her |
| Drift, arkitektur, kode- og KI-dokumentasjon | Denne README-en og kommentarer ved sentral kode | Sammenhold kjøreinstruksjoner, testnavn og faktisk kildekode; gruppa kontrollerer egen KI-beskrivelse |

### Sikkerhet

Identity hasher passord, krever minst åtte tegn med stor/liten bokstav og tall, og låser konto i fem minutter etter fem feil. Cookies er HttpOnly/SameSite=Lax og Secure i Production. Innlogging/registrering har i tillegg en grense på 60 POST-kall per minutt per IP. CSP med nonce begrenser scripts, og autentiserte HTML-/JSON-svar sendes med no-store. POST-handlinger har antiforgery, og rolle-/eierskapskontroll utføres på serveren. Registreringsfelt kan ikke gi adminrolle eller endre ressursens eier. Brukerinnhold vises kodet, også i kartpopup. Database-/kontopassord ligger i lokal konfigurasjon, ikke i repoet. Web kjører som ikke-root-bruker i containeren.

Tofaktor er utelatt etter foreleserens avklaring, formidlet av gruppa 5. november 2026. Vanlig innlogging og autorisasjon er fortsatt implementert. Compose er et lokalt Development-oppsett bundet til localhost med HTTP, ikke en ferdig produksjonsdeploy. Produksjon krever blant annet HTTPS/reversproxy, sikker hemmelighetshåndtering og separat drift/backup.

## Tester og gjentakbare resultater

### Enhetstester og JavaScript

```powershell
dotnet restore WebApplicationInAspire.slnx --locked-mode
dotnet build WebApplicationInAspire.slnx --configuration Release --no-restore
dotnet test WebApplicationInAspire.Tests --configuration Release --no-build --filter "Category!=Integration"
node --test WebApplicationInAspire.Tests/resource-position.test.cjs WebApplicationInAspire.Tests/area-picker.test.cjs
```

### HTTP-/system-/sikkerhetstester mot isolert MariaDB

Bruk bare en separat testserver. Testene oppretter og sletter sine egne databaser med prefikset `is202_test_`; de skal aldri kjøres mot prosjektets arbeidsdatabase. Standardoppsettet under deler verken volum eller port med appens database.

```powershell
$env:TEST_DB_PASSWORD = "velg-et-lokalt-testpassord"
docker compose -p is202-tests -f compose.tests.yaml up -d --wait
$env:TEST_DB_CONNECTION_STRING = "Server=127.0.0.1;Port=43325;User ID=root;Password=$env:TEST_DB_PASSWORD;Database=is202_test;"
dotnet test WebApplicationInAspire.slnx --configuration Release --no-build --logger "trx;LogFileName=tests.trx" --results-directory TestResults
docker compose -p is202-tests -f compose.tests.yaml down
```

På macOS/Linux: bruk `export TEST_DB_PASSWORD='...'` og `export TEST_DB_CONNECTION_STRING="Server=127.0.0.1;Port=43325;User ID=root;Password=$TEST_DB_PASSWORD;Database=is202_test;"`. Når variabelen ikke er satt, rapporteres integrasjonstestene som **Skipped**, ikke Bestått. CI setter variabelen og kjører alle testene mot en MariaDB-service. Build og test bruker samme Release-konfigurasjon. Testresultater lagres som TRX-artifact i GitHub Actions; Docker-imaget bygges også.

**Faktisk lokal kjøring 5. november 2026:** Release-build av hele løsningen: 0 feil/0 advarsler; **77/77 .NET-tester og 13/13 JavaScript-tester bestått, ingen hoppet over**. Docker Compose med web/MariaDB startet. EF rapporterte ingen modellendringer uten migrasjon. GitHub Actions-resultatet for den aktuelle PR-en må leses under Checks; denne lokale kjøringen alene beviser ikke et grønt CI-resultat.

| Testområde / testnavn | Steg og forventning | Faktisk lokalt resultat |
|---|---|---|
| `ValidationTests`, `ResourceControllerTests` (33) | Send manglende/omvendt tidsrom, ugyldige koordinater, for lange felt, feil eier og rollekombinasjoner; avvis feil og ikke lagre. | Bestått |
| `Registration_stores_hashed_password_and_cannot_choose_admin_role` | Registrer via HTTP med forsøkt adminfelt; hash lagres, bare leverandørrolle opprettes. | Bestått |
| `Resource_CRUD_preserves_owner_period_and_database_values` | Registrer/rediger/les/slett med ekte cookies/token; kontroller eier, tidsrom og databaseverdier. GET skal ikke slette. | Bestått |
| `Invalid_period_and_coordinates_return_errors_without_insert` | Send ugyldige datoer/koordinater via HTTP; skjema/feil vises og ingen rad opprettes. | Bestått |
| `Roles_ownership_CSRF_and_encoded_content_are_enforced_by_HTTP_pipeline` | Anonym/feil eier/rolle avvises; manglende token gir 400; scripttekst gjengis kodet. | Bestått |
| `Public_and_regional_overview_hide_private_unavailable_and_legacy_periods` | Sammenlign leverandør/offentlig oversikt og utskrift; skjulte tilbud skal ikke lekke. | Bestått |
| `Login_rejects_external_return_url_and_locks_after_five_failures` | Prøv ekstern returadresse og fem feilpassord; ingen ekstern redirect, konto låses. | Bestått |
| `Need_HTTP_flow_matches_assigns_releases_and_resolves` | Opprett behov, match, tildel, frigi, tildel igjen og løs; status og tilgjengelighet følger hverandre. | Bestått |
| `Matching_honors_period_boundaries_and_rejects_forged_out_of_period_assignment` | Test start/sluttgrense og tilbud som slutter for tidlig; feil tidsrom kan ikke tildeles med direkte kall. | Bestått |
| `Concurrent_needs...`, `Same_need_claim...` | To behov konkurrerer om én ressurs og to ressurser om ett behov; nøyaktig én vinner, tapende endring rulles tilbake, tildelt ressurs kan ikke endres/slettes. | Bestått |
| `Data_survives_new_application_host...`, `Upgrade_from_previous_release...` | Start ny appinstans og oppgrader tidligere migrasjon med eldre ressurs; bevar data og la ukjente datoer være null. | Bestått |
| `resource-position.test.cjs` (4), `area-picker.test.cjs` (9) | Kartbortfall, manuelle koordinater, sperret skjema, drag-preview/fullføring, avbrutt/for liten drag, to hjørner, gjenopprettet kartflytting, punktbytte og bevart område ved flisfeil. Komponenttester uten faktisk nettleser. | Bestått |

Nye integrasjonstester dekker også doble registreringskall, utdaterte skjemaer, private varsler, SQL-sortert avstand, behovsredigering, 10 000 ressurser/5 000 behov, gammel tildelingsbekreftelse, redigering samtidig med tildeling, komplett CSV-eksport/eierskap/formelvern og sikkerhetsheadere/tilgjengelige serverfeil. Datamengdetesten dokumenterer avgrenset sidestørrelse, ikke et løfte om et bestemt antall samtidige brukere.

Nettleserkontroll 5. november 2026 med syntetiske data: matching, bekreftelse og tildeling, status/historikk, leverandørens varsel og kontaktvisning, markering som lest, søk og kart, tomt registreringsskjema med lenkede feil og smal mobilvisning. Automatisert kontroll er ikke en personstudie eller full tilgjengelighetsrevisjon. Denne nettleserkontrollen gjaldt før den nye offentlige toppmenyen og områdevelgeren. Visuell kontroll av disse er foreløpig ikke utført fordi nettlesertilgang ble deaktivert i Work; se UX-VERIFISERING.md.

### Brukervennlighetstest - må utføres av gruppa

**Ikke gjennomført eller dokumentert som personstudie i denne PR-en.** La en person som ikke har bygget funksjonen prøve med syntetiske data, gjerne på faktisk mobil:

1. Registrer en ressurs med tidsrom og posisjon.
2. Rett et ugyldig tidsrom eller manglende posisjon uten hjelp.
3. Som offentlig bruker: finn match, tildel og frigi en ressurs.
4. Finn regional liste og forklar ressursens status.

Noter dato, testet commit/enhet, oppgave, tid, fullført uten hjelp, feil/stoppunkter og forbedring. Fyll inn faktiske observasjoner i README før dere påstår at dette oppgavekravet er gjennomført. Ikke skriv oppdiktede deltakere eller resultater.

## Demo til sprint review 5

Start Compose og klargjør én leverandør og én offentlig demobruker. Bruk samme type/region og la behovets tidspunkt ligge innenfor ressursens tidsrom.

Vis registrering/kartpunkt og manuell reserve, lagrede data, redigering, rolleforskjell, behov, matching, tildeling, tidlig frigjøring og Løst, regionfilter/utskrift og mobilvisning. Vis at en tildelt ressurs ikke kan slettes. Åpne GitHub Actions-testresultatene. Det separate visuelle designutkastet fra chatten er ikke en del av denne branchen.

## Bruk av KI

KI er brukt som støtte til planlegging, kodeforslag, feilsøking, kodegjennomgang, tester og dokumentasjon. Den tidligere dokumentasjonen oppga GitHub Copilot til MVC/Leaflet, koordinatvalidering og Docker-feilsøking. KI-støttet testing erstatter ikke brukervennlighetstesting med faktiske personer.

## Avgrensninger og videre arbeid

- Reell personbasert brukervennlighetstest og dens resultater gjenstår som beskrevet over.
- Matching bruker kategori, region eller kartområde og behovets tidspunkt, med luftlinjesortering når posisjon finnes. E-postvarsling, vedlegg og en full sikkerhetslogg er ikke implementert; tildelingshistorikk lagres som beskrevet over.
- Ressursenes tilgjengelighet vedlikeholdes manuelt. Ingen automatisk bekreftelse mot fysiske forhold.
- Ressursens tidsrom dekker ett tidspunkt for behovet; booking av flere tidsperioder og behovsvarighet støttes ikke.
- Norske lokale tidspunkter kan være tvetydige ved sommertid; løsningen er ikke sertifisert for operativ krisehåndtering eller produksjonsdrift.
