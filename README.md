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

## Configuratie

| Instelling | Waar | Voorbeeld |
|---|---|---|
| Database | `ConnectionStrings:Default` (`app.db` in de projectmap) | `Data Source=app.db` |
| Beheerderswachtwoord (productie) | omgevingsvariabele `Beheer__Wachtwoord` | `Beheer__Wachtwoord="Stark!Wachtwoord123"` |
| Beheerderswachtwoord (lokale dev) | `appsettings.Development.json` → `Beheer:Wachtwoord` | dev-wachtwoord staat daar al |

**Belangrijk:** in `appsettings.json` staat bewust géén echt wachtwoord. In productie
(deploy-server) moet je als omgevingsvariabele `Beheer__Wachtwoord` instellen, anders
weigert de login met een duidelijke foutmelding.

## Beveiliging

- Cookie-authenticatie met HttpOnly-, Secure- en SameSite-vlaggen; de auth-cookie is 8 uur geldig.
- Het inlogformulier heeft een snelheidslimiet: maximaal 5 pogingen per 15 minuten per IP-adres.
- Alle POST-acties (beheer én contactformulier) valideren antiforgery-tokens.
- Gebruikte antiforgery-cookieversleuteling / data protection staat onder de App_Data van de host.
- Security-headers op elke reactie: `X-Content-Type-Options`, `X-Frame-Options: DENY`,
  `Referrer-Policy`, `Permissions-Policy` en een Content-Security-Policy
  (o.a. `frame-ancestors 'none'`, `object-src 'none'`).
- Beheerpagina's sturen `Cache-Control: no-store` zodat ze niet in de browser- of proxycache blijven.
- De beheerderswachtwoordvergelijking gebeurt constant-time om timingaanvallen te blokkeren.
- Uploads: witte lijst van extensies (geen `.svg`, die kan scriptcode bevatten), limieten
  per soort (8 MB beeld, 64 MB video, 16 MB pdf) en een willekeurige bestandsnaam;
  padtraversal is onmogelijk omdat alleen de bestandsnaam uit het publieke pad wordt gebruikt.

## Deploy naar productie

1. Stel de omgevingsvariabele `Beheer__Wachtwoord` in op de server.
2. Bouw met `dotnet publish -c Release` of gebruik de gitploy van de host.
3. De SQLite-database (`app.db`) blijft op de server staan; bij een update wordt die
   automatisch bijgewerkt/ingevuld door de initializer.
