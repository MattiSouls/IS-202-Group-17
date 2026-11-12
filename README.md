# **Beredt**

## Gruppe 17

| Navn                   | E-post                                        | GitHub                                        |
|------------------------|-----------------------------------------------|-----------------------------------------------|
| Yousif Obaid           | [Yousifo@uia.no](mailto:Yousifo@uia.no)       | [Yousif0baid](https://github.com/Yousif0baid) |
| Lyubo Petrov           | [Lyubomirmp@uia.no](mailto:Lyubomirmp@uia.no) | [Lubo-mp](https://github.com/Lubo-mp)         |
| Idar Golf              | [Idarg@uia.no](mailto:Idarg@uia.no)           | [Idar770](https://github.com/Idar770)         |
| Mathias Ghebreselassie | [Mathiasyg@uia.no](mailto:Mathiasyg@uia.no)   | [MattiSouls](https://github.com/MattiSouls)   |
| Vu Truong Le           | [Vutl@uia.no](mailto:Vutl@uia.no)             | [Vu1308](https://github.com/Vu1308)           |
| Birk Nygård Børseth    | [Birknb@uia.no](mailto:Birknb@uia.no)         | [Birkkkk](https://github.com/Birkkkk)         |

## Prosjektmål

Målet med prosjektet er å lage en rask, sikker og brukervennlig webapplikasjon som kobler offentlige beredskapsbehov med ressurser fra leverandører. Ressursleverandører registrerer hva de har tilgjengelig, hvor og når. Offentlige aktører registrerer behov, finner ressurser som passer på kategori, geografi og tid, og tildeler dem. Løsningen er en ASP.NET Core MVC-applikasjon med MariaDB, ASP.NET Core Identity, Leaflet og Docker Compose.

## Installasjon / oppsett

> [!CAUTION]
> ### 0. Forutsetninger
> - Git
> - Docker Desktop med Linux-containere (må kjøre før oppstart)
> - Port `8080` (web) og `3307` (database) er ledige. Begge kan endres i `.env`.

### 1. Klon repoet

```powershell
git clone https://github.com/MattiSouls/IS-202-Group-17.git
cd IS-202-Group-17
```

### 2. Lag `.env`-fil

```powershell
Copy-Item .env.example .env
```

På macOS/Linux: `cp .env.example .env`

Åpne `.env` og sett egne passord for `MARIADB_PASSWORD` og `MARIADB_ROOT_PASSWORD`.

> [!IMPORTANT]
> Fyll også inn testkontoene i `.env` før oppstart. Uten dem kan du bare registrere deg som ressursleverandør, og rollene Offentlig aktør og Administrator kan ikke testes.
>
> ```env
> SEED_PUBLIC_EMAIL=offentlig@test.no
> SEED_PUBLIC_PASSWORD=Offentlig123
> SEED_ADMIN_EMAIL=admin@test.no
> SEED_ADMIN_PASSWORD=Admin1234
> ```
>
> Passord må ha minst 8 tegn, minst én stor bokstav, én liten bokstav og ett tall. Testkontoene opprettes bare i Development, som er standard i `compose.yaml`.

### 3. Start applikasjonen

```powershell
docker compose up -d --build --wait
docker compose ps
```

Åpne **http://localhost:8080**. Appen og MariaDB kjører i hver sin container, og EF-migrasjonene kjøres automatisk ved oppstart. Du trenger ikke kjøre SQL-skript manuelt.

### 4. Logg inn

| Bruker | Passord | Rolle |
|---|---|---|
| Verdien i `SEED_PUBLIC_EMAIL` | Verdien i `SEED_PUBLIC_PASSWORD` | Offentlig aktør |
| Verdien i `SEED_ADMIN_EMAIL` | Verdien i `SEED_ADMIN_PASSWORD` | Administrator |
| Registrer ny bruker i appen | Valgfritt | Ressursleverandør |

> [!WARNING]
> Kontoen låses i fem minutter etter fem feil passordforsøk.

<details>
<summary><b>Drift: logger, stopp, start og oppdatering</b></summary>

```powershell
docker compose logs --tail 100 web mariadb
docker compose stop
docker compose start --wait
```

Ved oppdatering: hent siste kode og kjør `docker compose up -d --build --wait`. Data og innloggingsnøkler beholdes i Docker-volumer. `docker compose down -v` sletter volumene, så ta databasebackup før oppgradering av viktige data. Compose er satt opp for lokal kjøring; ekstern drift krever HTTPS og egen driftskonfigurasjon.
</details>

### Mulige problemer og løsninger

1. **`Create .env before starting Compose`**
   - `.env` mangler eller passordfeltene er tomme. Gjør steg 2 på nytt.
2. **Porten er allerede i bruk**
   - Endre `WEB_PORT` eller `DB_PORT` i `.env` og start på nytt.
3. **Testkontoene virker ikke**
   - Sjekk at `SEED_*` var fylt inn før første oppstart, og at passordene følger kravene over. Start på nytt med `docker compose up -d --wait`.
4. **Ren start**
   - `docker compose down -v` og deretter `docker compose up -d --build --wait`. Dette sletter alle data.

## Roller og funksjoner

| Rolle | Tilgang |
|---|---|
| Ressursleverandør | Registrere, se, redigere og slette egne ressurser. |
| Offentlig aktør | Registrere behov, se tilbud og behov, matche og håndtere egne tildelinger. |
| Administrator | Administrere alle ressurser og behov. |

Vanlig registrering oppretter en ressursleverandør. Alle ressurs- og behovssider krever innlogging. Bare eier/oppretter eller administrator kan endre data.

Ressurser registreres med kategori, region, tidsrom, kontakt og kartpunkt. Behov registreres med kategori, region, tidspunkt, prioritet, kontakt og eventuelt punkt eller kartområde. Koordinater kan også fylles inn manuelt. Tidspunkter oppgis i norsk lokal tid.

Matching krever samme kategori, riktig region eller kartområde, et dekkende tidsrom og en ledig ressurs. Treff sorteres etter luftlinje når behovet har posisjon. Tilgjengelighet er oppgitt av leverandøren; kapasitet og praktisk bruk avtales direkte.

Behov bruker statusene Ny, Vurderes, Tildelt og Løst. Tildeling sperrer ressursen; frigivelse gjør den ledig igjen. Varsler sendes i portalen. Ressursoversikten har søk, filtre, kart og sideinndeling. Regionlister kan skrives ut eller eksporteres som CSV.

## Arkitektur

.NET 10 / ASP.NET Core MVC, Razor, Bootstrap, Leaflet/OpenStreetMap, EF Core 9/Pomelo og MariaDB 11.4.

```text
Nettleser: skjema og Leaflet-kart
  -> MVC-controller: GET/POST, validering og tilgangskontroll
  -> Repository / AllocationService
  -> ApplicationDbContext og EF-migrasjoner
  -> MariaDB i Docker
```

Viewmodeller validerer skjemaene, og Razor-visninger viser dataene. Repositoryene håndterer datatilgang. `AllocationService` håndterer matching og lagrer tildeling, status, varsler og historikk i én transaksjon. Versjonskontroll og betingede SQL-oppdateringer hindrer overskriving og doble tildelinger. Vellykket POST følges av redirect til en GET-side. Sentrale klasser og metoder har XML-kommentarer i koden.

## Sikkerhet

Identity lagrer passord som hash og låser kontoen i fem minutter etter fem feil. Serveren kontrollerer roller, eierskap, skjemadata og antiforgery-token på POST. Brukerinnhold vises kodet. CSP, sikre cookie-innstillinger og begrensning av innloggingsforsøk er konfigurert. Passord ligger i lokal konfigurasjon og skal ikke committes. Tofaktor er utelatt etter avklaring med foreleser.

## Testing

Siste lokale automatiserte kjøring 5. november 2026: **77/77 .NET-tester og 13/13 JavaScript-tester bestått**, ingen hoppet over. Release-build: 0 feil og 0 advarsler.

GitHub Actions bygger og tester Release mot MariaDB, kontrollerer migrasjoner og bygger Docker-imaget. TRX-resultater lagres som artifacts under [GitHub Actions](https://github.com/MattiSouls/IS-202-Group-17/actions).

<details>
<summary><b>Kjør testene lokalt</b></summary>

> [!CAUTION]
> Krever .NET 10 SDK, Node.js 22+ og en separat MariaDB-testserver. Kommandoene under starter en isolert testdatabase. Bruk aldri prosjektets arbeidsdatabase til testene.

```powershell
dotnet restore WebApplicationInAspire.slnx --locked-mode
dotnet build WebApplicationInAspire.slnx --configuration Release --no-restore
$env:TEST_DB_PASSWORD = "velg-et-lokalt-testpassord"
docker compose -p is202-tests -f compose.tests.yaml up -d --wait
$env:TEST_DB_CONNECTION_STRING = "Server=127.0.0.1;Port=43325;User ID=root;Password=$env:TEST_DB_PASSWORD;Database=is202_test;"
dotnet test WebApplicationInAspire.slnx --configuration Release --no-build --logger "trx;LogFileName=tests.trx" --results-directory TestResults
node --test WebApplicationInAspire.Tests/resource-position.test.cjs WebApplicationInAspire.Tests/area-picker.test.cjs
docker compose -p is202-tests -f compose.tests.yaml down
```

Uten `TEST_DB_CONNECTION_STRING` hoppes integrasjonstestene over.
</details>

<details>
<summary><b>Testscenarioer</b></summary>

| Testscenario | Steg | Forventet resultat | Faktisk resultat |
|---|---|---|---|
| Brukerregistrering | Registrer bruker og prøv å sende inn en adminrolle. | Bruker lagres med passordhash og bare leverandørrolle. | Bestått |
| Ressurs-CRUD | Registrer, les, rediger og slett med innlogging og gyldig token. | Riktige databaseverdier og eier beholdes; GET sletter ikke. | Bestått |
| Validering | Send manglende/omvendt tidsrom og ugyldige koordinater. | Feil vises og ingen ugyldig rad lagres. | Bestått |
| Tilgang og sikkerhet | Prøv anonym tilgang, feil eier, manglende token og scriptinnhold. | Uautoriserte endringer avvises og innhold vises kodet. | Bestått |
| Matching og status | Opprett behov, finn treff, tildel, frigi og løs. | Kategori, geografi og tid kontrolleres; status og ledighet følger hverandre. | Bestått |
| Samtidighet | La to behov konkurrere om samme ressurs og send utdaterte skjemaer. | Én tildeling lykkes; gamle skjemaer gir konflikt. | Bestått |
| Varig lagring | Start ny appinstans og oppgrader fra tidligere databaseskjema. | Data beholdes og migrasjoner lykkes. | Bestått |
| Kart og områdevalg | Prøv grenser, kartbortfall, koordinatfelt og avbrutt tegning. | Gyldige valg beholdes; feil gir tilbakemelding uten å utvide søket. | Bestått |

Gruppa har i tillegg gjennomført manuell funksjons- og brukervennlighetstesting av registrering, feilretting, matching, tildeling, varsler, frigivelse og mobilvisning.
</details>

## Bruk av KI

KI er brukt som støtte til planlegging, kodeforslag, feilsøking, kodegjennomgang, tester og dokumentasjon. Den tidligere dokumentasjonen oppga GitHub Copilot til MVC/Leaflet, koordinatvalidering og Docker-feilsøking.
