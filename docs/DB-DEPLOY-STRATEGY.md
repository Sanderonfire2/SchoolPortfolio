# DB-migratiestrategie voor de serverdeploy

De server (admin.monsterasp.net) geeft `500.30` omdat de app bij opstartenst `DbContext.Database.Migrate()` aanroept en de eerste migratie faalt met:

    ALTER TABLE GallerijAfbeeldingen ADD COLUMN IsHoofdCover INTEGER NOT NULL DEFAULT 0
    SQLite Error 1: 'no such table: GallerijAfbeeldingen'

Oorzaak: de `__EFMigrationsHistory` tabel is leeg op een nieuwe server-DB, dus EF probeert de *oudste* migratie toe te passen, maar die oudste migratie doet `ALTER TABLE` op een tabel die nooit aangemaakt is. DB is nooit via EF aangemaakt op de server; alleen lokaal via `import-definitief.mjs` (raw DDL).

## Wat er op de server moet gebeuren

1. **Backup van bestaande DB** (indien aanwezig) via script `scripts/bootstrap-prod-db.ps1`.
2. **Leere SQLite-DB initialiseren** met de `InitialCreate` migratie zodat de basis-tabellen bestaan.
3. **Overige migraties** (`StagePovDagrapporten`, `VoegSoortToeAanGallerij`, `FixIsHoofdCoverColumn`, `SchooljaarEnVakBijProject`, `VoegSchooljaarToeAanStageEnPov`) worden dan in volgorde toegepast.

## Gebruikte scripts

- `scripts/bootstrap-prod-db.ps1` – productieserver (backup + Migrate).
- `scripts/bootstrap-dev-db.ps1` – lokale ontwikkelmachine.
- `bootstrap-prod-db.csx` – Roslyn-script variant voor servers zonder PS-hosting.

## Aandachtspunten

- De `InitialCreate` migratie is statisch aangemaakt en moet exact matchen met de EF-model. Controleer na aanmaken of `dotnet ef migrations list` de nieuwe migratie toont vóór deploy.
- Migrations volgorde is: `InitialCreate` → `StagePovDagrapporten` → `VoegSoortToeAanGallerij` → `FixIsHoofdCoverColumn` → `SchooljaarEnVakBijProject` → `VoegSchooljaarToeAanStageEnPov`.
- Zorg dat de app-pool identiteit schrijfrechten heeft op de DB-map (en `wwwroot` uploads map).
- Na migratiestap kun je `db.Database.EnsureCreated()` verwijderen of vervangen door een expliciete check; `Migrate()` kan veiliger zijn.
