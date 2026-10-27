-- Manual init: run with `sqlite3 SchoolPortfolio.db < scripts/SQLiteDB-ManualInit.sql`
-- Use this as a fallback to verify that EF Core can operate on the DB after init.

-- Safety seed: prevent __EFMigrationsHistory from being dropped by accident.
-- EF Core's Migrate() is authoritative for contents; this DDL exists only as a safety net.
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "ContactBerichten" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ContactBerichten" PRIMARY KEY AUTOINCREMENT,
    "Naam" TEXT NOT NULL,
    "Email" TEXT NOT NULL,
    "Bericht" TEXT NOT NULL,
    "Gelezen" INTEGER NOT NULL,
    "GereviseerdDoorGastschrijver" INTEGER NOT NULL,
    "Datum" TEXT NOT NULL,
    "BijgewerktOp" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "Opleidingen" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Opleidingen" PRIMARY KEY AUTOINCREMENT,
    "Naam" TEXT NOT NULL,
    "NaamNl" TEXT NOT NULL,
    "School" TEXT NULL,
    "Periode" TEXT NULL,
    "Omschrijving" TEXT NOT NULL,
    "KorteIntroductie" TEXT NOT NULL,
    "HasVakDag" INTEGER NOT NULL,
    "IsGepubliceerd" INTEGER NOT NULL,
    "Volgorde" INTEGER NOT NULL,
    "BijgewerktOp" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "PaginaInhouden" (
    "Sleutel" TEXT NOT NULL CONSTRAINT "PK_PaginaInhouden" PRIMARY KEY,
    "Inhoud" TEXT NOT NULL,
    "BijgewerktOp" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "Profielen" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Profielen" PRIMARY KEY AUTOINCREMENT,
    "Naam" TEXT NOT NULL,
    "Titel" TEXT NOT NULL,
    "AfbeeldingUrl" TEXT NULL,
    "OverMijTekst" TEXT NOT NULL,
    "KorteIntroductie" TEXT NOT NULL,
    "IsGepubliceerd" INTEGER NOT NULL,
    "BijgewerktOp" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "Stages" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Stages" PRIMARY KEY AUTOINCREMENT,
    "Bedrijf" TEXT NOT NULL,
    "Functie" TEXT NULL,
    "StartDatum" TEXT NULL,
    "EindeDatum" TEXT NULL,
    "Opdrachtgever" TEXT NULL,
    "Locatie" TEXT NULL,
    "Omschrijving" TEXT NOT NULL,
    "Schooljaar" TEXT NULL,
    "BijgewerktOp" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "Werkervaringen" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Werkervaringen" PRIMARY KEY AUTOINCREMENT,
    "Naam" TEXT NOT NULL,
    "Bedrijf" TEXT NULL,
    "Functie" TEXT NULL,
    "Periode" TEXT NULL,
    "Omschrijving" TEXT NOT NULL,
    "IsGepubliceerd" INTEGER NOT NULL,
    "Volgorde" INTEGER NOT NULL,
    "BijgewerktOp" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "Projecten" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Projecten" PRIMARY KEY AUTOINCREMENT,
    "Titel" TEXT NOT NULL,
    "Categorie" TEXT NOT NULL,
    "Omschrijving" TEXT NULL,
    "CoverUrl" TEXT NULL,
    "OpleidingEersteGraad" TEXT NULL,
    "OpleidingTweedeGraad" TEXT NULL,
    "Schooljaar" TEXT NULL,
    "Vak" TEXT NULL,
    "BijgewerktOp" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "GallerijAfbeeldingen" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_GallerijAfbeeldingen" PRIMARY KEY AUTOINCREMENT,
    "ProjectId" INTEGER NOT NULL,
    "Url" TEXT NOT NULL,
    "OpslagPad" TEXT NULL,
    "Soort" TEXT NOT NULL,
    "IsHoofdCover" INTEGER NOT NULL,
    CONSTRAINT "FK_GallerijAfbeeldingen_Projecten_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projecten" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_GallerijAfbeeldingen_ProjectId" ON "GallerijAfbeeldingen" ("ProjectId");
