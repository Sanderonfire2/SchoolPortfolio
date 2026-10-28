# Schoolportfolio — Sander Van Praet

ASP.NET MVC 10 (.NET) portfolio-site met SQLite en een ingebouwd beheerdersgedeelte (`/admin`).

## Opstarten

```bash
dotnet restore
dotnet run
```

Bij de eerste start maakt `DataInitializer` automatisch de database (`app.db`) aan en vult die
met de standaarddata (profiel, projecten, stages, POV-locaties en rapporten).
Ontbrekende rijen worden bij een herstart aangevuld, bestaande rijen blijven staan (idempotent).

## Inloggen op /admin

| Omgeving | Waar staat het wachtwoord? |
|---|---|
| Lokaal (`dotnet run`) | .NET user secrets (buiten de repository) |
| Productie | omgevingsvariabele `Beheer__Wachtwoord` of `appsettings.Production.json` |

Het beheerderswachtwoord staat nooit in de repository. Lokaal zet je het één keer in de
user-secrets-opslag van je eigen Windows-profiel:

```bash
dotnet user-secrets set "Beheer:Wachtwoord" "je-wachtwoord"
dotnet user-secrets list   # toont de ingestelde sleutels
dotnet run
```

`dotnet run` gebruikt het launchprofiel met `ASPNETCORE_ENVIRONMENT=Development`, en in die
omgeving laadt ASP.NET Core de user secrets automatisch. Op een verse clone moet je het
wachtwoord dus opnieuw instellen; er staat bewust geen standaardwachtwoord in de repo.

Op MonsterASP.NET stel je die variabele zo in:

1. Open het configuratiescherm van de website.
2. Ga naar **Scripting → Environment variables**.
3. Voeg `Beheer__Wachtwoord` toe met je eigen wachtwoord als waarde.
4. Klik **Restart application**.

Zonder die variabele start de site gewoon, maar toont `/admin/inloggen` een duidelijke melding
in plaats van een foutpagina. Een alternatief is een `appsettings.Production.json` op de server
(die staat in `.gitignore`, dus hij komt nooit op GitHub). Zet dat bestand op MonsterASP onder
**Protected files**, zodat een volgende git-deploy het niet vervangt.

Zet het wachtwoord nooit in `appsettings.json` of `appsettings.Development.json`: die bestanden
staan wél in de repository. Lokaal gebruik je user secrets, productie gebruikt de omgeving.

### 500 op /admin na een update?

De database herstelt zichzelf bij het starten. Ontbrekende tabellen en kolommen (bijvoorbeeld
`ContactBerichten.Beantwoord`) worden automatisch toegevoegd, ook wanneer de migratiegeschiedenis
van een oudere database niet meer klopt. Herstart de app na een deploy; daarna werkt `/admin`
weer. In de log verschijnt dan één regel met de kolommen die zijn toegevoegd.

## E-mail: echt versturen via Brevo vanaf je eigen adres

Mails gaan via de gratis SMTP-relay van [Brevo](https://www.brevo.com/) (300 mails per dag, geen
creditcard). De afzender is je eigen adres; de SMTP-gegevens staan nooit in de repository.

Eenmalig instellen:

1. Maak een gratis Brevo-account aan.
2. Voeg je afzenderadres toe onder **Settings → Senders, Domains & IPs → Senders** en verifieer
   het met de code die je per mail krijgt. Wil je vanaf het hele domein versturen, authenticeer dan
   het domein met de DNS-records (DKIM/DMARC) die Brevo toont.
3. Ga naar **Settings → SMTP & API → SMTP**, kopieer de **Login** en genereer een **SMTP-key**.
4. Vul op **/admin/email** je afzenderadres, de SMTP-login en de SMTP-key in en klik op
   **E-mail koppelen**.
5. Klik op **Testmail verzenden**. De mail wordt echt verstuurd; bij een fout zie je de melding van
   de Brevo-server zelf (verkeerde SMTP-key, niet-geverifieerde afzender, ...).

De koppeling wordt versleuteld bewaard in `App_Data/email-geheimen.json` met ASP.NET Core Data
Protection. Dat bestand staat niet in de repository en overleeft een herstart. Op een host zonder
schrijfbare `App_Data` kun je in plaats daarvan de omgevingsvariabelen `Email__Afzender`,
`Email__SmtpGebruiker` en `Email__SmtpWachtwoord` instellen.

De host moet uitgaande verbindingen op poort 587 (STARTTLS) naar `smtp-relay.brevo.com` toelaten.

## AREI-pdf's

De drie AREI-boeken staan in `wwwroot/downloaden/arei` en worden via `/arei/{slug}/pdf` geserveerd
(met ondersteuning voor range-requests, zodat de pdf-lezer van de browser werkt). Daardoor werken
“In de browser inlezen”, “Openen in nieuw tabblad” en “Downloaden (pdf)” ook wanneer een oud pad
in de database naar een andere host zou wijzen. Een vervangen pdf via **/admin/arei** komt in
`wwwroot/uploads/arei` terecht en wordt op dezelfde manier geserveerd.

## Configuratie

| Instelling | Waar | Voorbeeld |
|---|---|---|
| Database | `ConnectionStrings:Default` (`app.db` in de projectmap) | `Data Source=app.db` |
| Beheerderswachtwoord (productie) | omgevingsvariabele `Beheer__Wachtwoord` | `Beheer__Wachtwoord="…"` |
| Beheerderswachtwoord (lokale dev) | `appsettings.Development.json` → `Beheer:Wachtwoord` | dev-wachtwoord staat daar al |
| E-mailkoppeling (Brevo) | /admin/email (versleuteld in `App_Data/email-geheimen.json`) | afzender + SMTP-login + SMTP-key |
| E-mailkoppeling via omgeving | `Email__Afzender`, `Email__SmtpGebruiker`, `Email__SmtpWachtwoord` | — |

## Beveiliging

- Cookie-authenticatie met HttpOnly-, Secure- en SameSite-vlaggen; de auth-cookie is 8 uur geldig.
- Data Protection-sleutels staan in `App_Data/dp-keys`, zodat antiforgery-tokens en de auth-cookie
  een herstart en een deploy naar een andere map overleven.
- Het inlogformulier heeft een snelheidslimiet: maximaal 5 pogingen per 15 minuten per IP-adres.
- Alle POST-acties (beheer én contactformulier) valideren antiforgery-tokens.
- Security-headers op elke reactie: `X-Content-Type-Options`, `X-Frame-Options: SAMEORIGIN`,
  `Referrer-Policy`, `Permissions-Policy` en een Content-Security-Policy
  (met `frame-src 'self'` voor de AREI-pdf's en `frame-ancestors 'self'`).
- Beheerpagina's sturen `Cache-Control: no-store` zodat ze niet in de browser- of proxycache blijven.
- De beheerderswachtwoordvergelijking gebeurt constant-time om timingaanvallen te blokkeren.
- Het beheerderswachtwoord staat buiten de repository: lokaal in .NET user secrets, in productie
  als omgevingsvariabele (of in het genegeerde `appsettings.Production.json`).
- Uploads: witte lijst van extensies, limieten per soort (8 MB beeld, 64 MB video, 16 MB pdf)
  en een willekeurige bestandsnaam; padtraversal is onmogelijk omdat alleen de bestandsnaam
  uit het publieke pad wordt gebruikt. Geüploade SVG's worden gesaneerd (scripts,
  event-handlers en externe verwijzingen eruit) en met een sandbox-CSP geserveerd.
- E-mailgegevens (afzender, Brevo SMTP-login en SMTP-key) staan versleuteld in `App_Data` en nooit in de repository of in de browser.

## Deploy naar productie

1. Stel de omgevingsvariabele `Beheer__Wachtwoord` in op de server (of gebruik
   `appsettings.Production.json`, dat je onder **Protected files** zet).
2. Bouw met `dotnet publish -c Release` of gebruik de git-deploy van de host.
3. De SQLite-database (`app.db`) en de map `App_Data` blijven op de server staan; bij een update
   wordt de database automatisch bijgewerkt/aangevuld door de initializer.
4. Start de app opnieuw. De initializer voegt ontbrekende tabellen én kolommen toe, zodat een
   oudere database niet zorgt voor een 500 op `/admin`.
