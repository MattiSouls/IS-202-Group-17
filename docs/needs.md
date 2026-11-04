# Behov, database og status

`Need` lagres i `Needs` gjennom eksisterende `ApplicationDbContext` og et scoped
`INeedRepository`. Migrasjonen `AddNeeds` legger til tabellen og fremmednøkkel til
Identity-brukeren som opprettet behovet. Eksisterende ressurs- og Identity-tabeller beholdes.

## Kjøring

Bruk prosjektets `.env` og `docker compose up -d --build`. Compose anvender migrasjonene
ved oppstart. Behold det eksisterende databasevolumet når du oppdaterer appen.
For eksplisitt migrering uten Compose: sett `ConnectionStrings__DefaultConnection` lokalt,
kjør `dotnet tool restore`, deretter
`dotnet ef database update --project WebApplicationInAspire/WebApplicationInAspire.csproj`.

Vanlig registrering gir ressursleverandørrollen. For en lokal offentlig testbruker:
fyll ut `SEED_PUBLIC_EMAIL` og `SEED_PUBLIC_PASSWORD` i `.env`. For en valgfri administrator
brukes `SEED_ADMIN_EMAIL` og `SEED_ADMIN_PASSWORD`. Begge verdier for kontoen må være satt.
Passordet må oppfylle Identity-reglene. Seedingen gjelder bare Development;
ikke legg `.env` eller virkelige passord i Git.

## Tilgang og status

- Offentlige brukere (`PublicUser`) og administratorer kan registrere og se behov.
- Oppretter settes på serveren i `CreatedByUserId`; skjemaet kan ikke velge eier eller startstatus.
- Bare oppretteren eller administrator kan endre status. POST krever antiforgery-token.
- Startstatus er `New`. Manuelt kan den settes til `New`, `UnderReview` eller `Resolved`.
- `Assigned` skal settes sammen med en faktisk tildeling i Birk sin flyt. Den kan ikke velges
  eller overskrives fra det manuelle statusskjemaet. Repositoryet sjekker dette i SQL-oppdateringen.
- Tidspunkt lagres som `NeededAt` (DATETIME), slik det er valgt i skjemaet, uten UTC-konvertering.

Birk kan bruke `DbSet<Need> Needs`, `Need.Id`, `Type`, `Region`, `Location`, `NeededAt`,
`Priority` og `NeedStatuses` i matching/tildeling. Prioriteter er `Low`, `Medium`, `High`, `Urgent`.
Koordiner nye migrasjoner med databaseansvarlig. Tildeling og frigjøring av ressurser er ikke
implementert i denne branchen.

## Kontrollert 4. november 2026

Release-build og Docker-build besto. EF-verktøyet fant ingen utestående modellendringer.
Migrasjon fra tidligere schema ble testet med lagret bruker/ressurs, som ble beholdt.
44 HTTP-/databasekontroller besto etter retting, inkludert:

| Scenario | Forventet og faktisk resultat |
|---|---|
| Registrer gyldig behov som offentlig bruker | Lagres med riktig oppretter og `New`; omdirigeres til oversikten |
| Tomme/påtvungne/for lange verdier og ugyldig tidspunkt | Skjema med valideringsfeil; ingen rad opprettes |
| Ressursleverandør prøver behovssider eller POST | Ingen tilgang; ingen behov lagres |
| Annen offentlig bruker prøver å endre status | Ingen tilgang; status beholdes |
| Oppretter eller administrator endrer status | Endringen lagres og vises som valgt verdi |
| Manglende antiforgery-token | HTTP 400 |
| Ugyldig status, også manuelt `Assigned` | HTTP 400 |
| Manuell endring av et allerede tildelt behov | HTTP 409; `Assigned` beholdes |
| Stopp/start av app og database | Behov, status og tidligere ressurser beholdes |

Nettleseren ble også brukt til registrering og statusendring. Datovalideringen er kontrollert
både i nettleseren og på serveren. Testdata og passord var isolerte engangsdata.
Dette er system-/integrasjons- og utvalgte sikkerhetskontroller; full brukervennlighetstest
og gruppas enhetstester må samordnes med testansvarlig.
