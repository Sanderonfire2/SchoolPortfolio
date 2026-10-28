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
| Lokaal (`dotnet run`) | `appsettings.Development.json` → `Beheer:Wachtwoord` (dev-wachtwoord staat daar al) |
| Productie | omgevingsvariabele `Beheer__Wachtwoord` |

Op MonsterASP.NET stel je die variabele zo in:

1. Open het configuratiescherm van de website.
2. Ga naar **Scripting → Environment variables**.
3. Voeg `Beheer__Wachtwoord` toe met je eigen wachtwoord als waarde.
4. Klik **Restart application**.

Zonder die variabele start de site gewoon, maar toont `/admin/inloggen` een duidelijke melding
in plaats van een foutpagina. Een alternatief is een `appsettings.Production.json` op de server
(Die staat in `.gitignore`, dus hij komt nooit op GitHub.)

## E-mail: echt versturen via je eigen Gmail

Ga naar **/admin/email** en koppel je Gmail-adres:

1. Zet 2-stapsverificatie aan op <https://myaccount.google.com/security>.
2. Maak een App-wachtwoord aan op <https://myaccount.google.com/apppasswords>
   (kies bijvoorbeeld “Mail” en “Windows Computer”).
3. Vul je Gmail-adres en dat 16-tekens App-wachtwoord in bij /admin/email en klik op
   **Gmail koppelen**. Spaties in het App-wachtwoord mogen.
4. Klik op **Testmail verzenden**. De mail wordt echt verstuurd vanaf je Gmail-adres; bij een
   fout zie je de melding van de Gmail-server zelf (bijvoorbeeld een verkeerd App-wachtwoord).

De koppeling wordt versleuteld bewaard in `App_Data/gmail-geheimen.json` met ASP.NET Core Data
Protection. Dat bestand staat niet in de repository en overleeft een herstart. Op een host zonder
schrijfbare `App_Data` kun je in plaats daarvan `Gmail__Adres` en `Gmail__AppWachtwoord` als
omgevingsvariabelen instellen.

De host moet uitgaande verbindingen op poort 587 (STARTTLS) naar `smtp.gmail.com` toelaten.

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
| Gmail-koppeling | /admin/email (versleuteld in `App_Data/gmail-geheimen.json`) | — |

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
- Uploads: witte lijst van extensies (geen `.svg`, die kan scriptcode bevatten), limieten
  per soort (8 MB beeld, 64 MB video, 16 MB pdf) en een willekeurige bestandsnaam;
  padtraversal is onmogelijk omdat alleen de bestandsnaam uit het publieke pad wordt gebruikt.
- Gmail-gegevens staan versleuteld in `App_Data` en nooit in de repository of in de browser.

## Deploy naar productie

1. Stel de omgevingsvariabele `Beheer__Wachtwoord` in op de server.
2. Bouw met `dotnet publish -c Release` of gebruik de git-deploy van de host.
3. De SQLite-database (`app.db`) en de map `App_Data` blijven op de server staan; bij een update
   wordt de database automatisch bijgewerkt/aangevuld door de initializer.
