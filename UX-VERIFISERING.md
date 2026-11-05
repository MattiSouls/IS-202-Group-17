# Brukerflyt og verifisering

Endringen bygger på main og beholder rollefordelingen: leverandør eier ressurser; offentlig bruker håndterer egne behov; administrator kan håndtere begge.

## Designvalg

| Problem | Konkret valg | Kontroll |
|---|---|---|
| For mange ressurser samtidig | SQL-paginering med 10 rader, søk og filtre; detaljer åpnes separat; kart skjules bak en utvidbar seksjon. | Test med 10 000 ressurser og 5 000 behov; sidestørrelse og HTML-mengde kontrolleres. Dette er en datamengdetest, ikke en måling av samtidige brukere. |
| Uklare treff | Samme kategori, region og dekkende tidsrom er obligatorisk. Treff sorteres etter luftlinje når behovet har koordinater, ellers navn/id. | MariaDB-test av rekkefølge og tidsgrenser; fire kriterier vises på hvert treff. |
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
- 55 av 55 .NET-tester bestått mot en isolert MariaDB, ingen hoppet over; 4 av 4 JavaScript-tester bestått.
- EF rapporterte ingen modellendringer uten migrasjon. Oppgradering fra tidligere skjema bevarer eldre ressursdata.
- Docker-web og MariaDB startet med helsesjekker. Varsler og tildeling var beholdt etter ombygging/omstart.
- Nettleser med syntetiske kontoer: matching → bekreftelse → tildeling → leverandørvarsel/kontakt → marker lest → frigivelse → status/historikk.
- Registrering med ugyldig og deretter gyldig navn, maksimal navnelengde, manuelle koordinater og tidsrom; verdier beholdt ved feil. Klientfeil flytter fokus til oppsummeringen, og feillenke flytter fokus til feltet.
- Ved 320 CSS-piksler hadde ressursliste/skjema ingen horisontal side-overflow, heller ikke med et 200 tegn langt ord. Skrivebordsvisning kontrollert ved 1280 piksler. Ingen konsollfeil observert i den kontrollerte nettleserøkten.
- Uavhengig personstudie, skjermleserrevisjon, reell nettverks-/lasttest og ekstern produksjonsdeploy er ikke gjennomført av disse kontrollene.
