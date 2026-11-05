# Drift og oppgradering

## Lokalt miljø

`compose.yaml` er et Development-miljø på localhost. Det publiserer ikke portalen på internett. Bruk oppstarten i README. Hemmeligheter og seed-passord skal ligge i lokal konfigurasjon, aldri i Git eller delte overleveringsfiler.

Ved oppgradering av et miljø med viktige data:

1. Noter gjeldende commit og databaseversjon. Stans nye brukerendringer under oppgraderingen.
2. Ta en verifisert MariaDB-backup før du starter ny kode. Ta også vare på Data Protection-volumet, med samme tilgangsbeskyttelse som andre hemmeligheter.
3. Kjør oppgraderingen i et separat miljø først. De nye migrasjonene legger til varsler, tildelingshistorikk, registreringsnøkler, versjonsfelt og valgfrie behovskoordinater. De fyller ikke inn oppdiktede koordinater eller tidsrom for eldre data.
4. Kjør `docker compose up -d --build --wait`, kontroller `docker compose ps` og `docker compose logs --tail 100 web mariadb` lokalt. Ikke del rå logger med passord eller kontaktdata.
5. Prøv innlogging og én komplett registrering/tildeling/frigivelse med avtalte testdata. Bekreft mottakernes varsler og at eldre data er beholdt.

Eksempel på backup i Linux-skallet inne i databasecontaineren:

```sh
mariadb-dump --single-transaction --routines --events --user=root --password="$MARIADB_ROOT_PASSWORD" "$MARIADB_DATABASE" > /tmp/portal-backup.sql
```

Kjør dette via `docker compose exec mariadb sh`, og kopier deretter filen til en beskyttet lagringsplass med `docker compose cp mariadb:/tmp/portal-backup.sql <lokal-backupfil>`. Backupen inneholder bruker- og kontaktdata. Kontroller at filen er ikke-tom og gjenopprett den i en separat testdatabase før du kaller backupen verifisert. En backup som bare finnes inne i containeren er ikke tilstrekkelig. Fastsett frekvens, oppbevaring og ansvar før reell drift.

Bruk ikke `docker compose down -v` på en database som skal beholdes. Ved feil etter migrering: bevar feiltilstanden for undersøkelse, stans skrivninger og bruk en testet gjenopprettingsplan. Å gå tilbake til gammel kode er ikke automatisk en database-rollback.

## Ekstern drift må konfigureres for valgt hosting

Hosting og meldingstjeneste er ikke konfigurert av denne endringen. Varsling er kun i portalen. Før ekstern drift må driftsansvarlig etablere HTTPS, riktig proxy-/videresendte headers-oppsett, Production-miljø, hemmelighetshåndtering, tilgang til databasen, overvåking og testet backup/gjenoppretting. Appen krever Secure-cookies i Production; å publisere Development-HTTP-oppsettet er ikke en erstatning for dette.

Innlogging/registrering har en grense på 60 POST-kall per minutt per IP, og Identity låser én konto etter fem feil. Bak en proxy uten riktig klient-IP-oppsett deler brukere proxyens grense; ikke stol på vilkårlige `X-Forwarded-For`-headers. Konfigurer bare kjente proxyer, og test ekstern HTTPS-innlogging og cookie-flagg i det faktiske miljøet. Flere appinstanser krever også en felles begrensningsstrategi; minnebasert ratebegrensning gjelder per instans.

Offentlige kontoer og administratorer tildeles av en ansvarlig administrator, aldri ved å stole på registreringsfelt fra nettleseren. Avtal en kontrollert rutine for kontotildeling, feilregistrering og passordgjenoppretting. Seed-kontoer er for oppsett/test; standardpassord skal ikke brukes i drift.

Behov og ressursperioder lagres som norsk lokal tid uten separat tidssonevalg. Tvetydige tidspunkter ved overgang til vintertid er ikke entydig modellert; dette må avklares før bruk som krever presis tidsplanlegging over slike overganger.

Avklar behandlingsansvar, lagringstid, tilgang og sletting av kontaktdata, varsler og historikk med den faktiske organisasjonen. Personvernsiden beskriver teknisk databruk; den erstatter ikke organisasjonens kontaktopplysninger eller beslutninger om behandlingsgrunnlag og lagringstid.
