using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolPortfolio.Models;

namespace SchoolPortfolio.Data;

/// <summary>
/// Zorgt bij opstarten voor het databaseschema én vult ontbrekende standaarddata
/// in (idempotent). De complete gesynchroniseerde dataset staat in StandaardData.Json
/// (geëxporteerd uit de productiedatabase met export-standaarddata.mjs): alle
/// projecten, galerijafbeeldingen, opleidingen, POV-locaties en dagrapporten worden
/// op een verse server automatisch terug opgevuld bij een `git pull` + herstart.
/// </summary>
public static class DataInitializer
{
    public static void Initialiseer(IServiceProvider services)
    {
        using var db = services.GetRequiredService<SiteDbContext>();

        // 1. Schema: EnsureCreated + Migrate (kan niet op een schone server falen).
        try
        {
            db.Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            Log(services, LogLevel.Error, ex, "EnsureCreated() mislukt.");
        }

        try
        {
            db.Database.Migrate();
        }
        catch (Exception ex)
        {
            Log(services, LogLevel.Error, ex, "Migrate() mislukt.");
        }

        if (!db.Database.CanConnect())
        {
            Log(services, LogLevel.Warning, null, "Geen verbinding met de database; standaarddata wordt overgeslagen.");
            return;
        }

        // 2. Idempotent seeden: rijen die al bestaan worden nooit aangepast of verwijderd,
        //    zodat admin-wijzigingen op de server veilig zijn.
        try
        {
            Seed(db);
        }
        catch (Exception ex)
        {
            Log(services, LogLevel.Error, ex, "Standaarddata-injectie mislukt; de site start zonder volledige data.");
        }
    }

    private static void Seed(SiteDbContext db)
    {
        var data = StandaardData.Lees<StandaardDataset>();
        var nu = DateTime.Now;

        // --- Profiel ---
        if (!db.Profielen.Any() && data.Profiel is { } profiel)
        {
            db.Profielen.Add(new Profiel
            {
                VolledigeNaam = profiel.VolledigeNaam,
                Tagline = profiel.Tagline ?? "",
                KorteIntroductie = profiel.KorteIntroductie,
                OverMijTekst = profiel.OverMijTekst,
                Woonplaats = profiel.Woonplaats,
                Email = profiel.Email,
                Telefoon = profiel.Telefoon,
                LinkedInUrl = profiel.LinkedInUrl,
                ProfielfotoPad = profiel.ProfielfotoPad,
                BijgewerktOp = nu
            });
        }

        // --- Projecten (op slug) ---
        var projectenBySlug = db.Projecten
            .ToDictionary(p => string.IsNullOrWhiteSpace(p.Slug) ? p.Titel : p.Slug!, p => p);

        foreach (var p in data.Projecten)
        {
            var key = string.IsNullOrWhiteSpace(p.Slug) ? p.Titel : p.Slug;
            if (projectenBySlug.ContainsKey(key)) continue;

            var project = new Project
            {
                Titel = p.Titel,
                Slug = p.Slug,
                Categorie = p.Categorie,
                Schooljaar = p.Schooljaar,
                Vak = p.Vak,
                Periode = p.Periode,
                Rol = p.Rol,
                Technologieen = p.Technologieen,
                KorteSamenvatting = p.KorteSamenvatting,
                Omschrijving = p.Omschrijving,
                Uitdaging = p.Uitdaging,
                Geleerd = p.Geleerd,
                CoverPad = p.CoverPad,
                IsUitgelicht = p.IsUitgelicht,
                IsGepubliceerd = p.IsGepubliceerd,
                Volgorde = p.Volgorde,
                BijgewerktOp = nu
            };
            db.Projecten.Add(project);
            projectenBySlug[key] = project;
        }

        // --- Opleidingen (op titel) ---
        var opleidingTitels = db.Opleidingen.Select(o => o.Titel).ToHashSet();
        foreach (var o in data.Opleidingen)
        {
            if (opleidingTitels.Contains(o.Titel)) continue;
            db.Opleidingen.Add(new Opleiding
            {
                Periode = o.Periode,
                Titel = o.Titel,
                Instelling = o.Instelling,
                Locatie = o.Locatie,
                Omschrijving = o.Omschrijving,
                IsGepubliceerd = o.IsGepubliceerd,
                Volgorde = o.Volgorde
            });
            opleidingTitels.Add(o.Titel);
        }

        // --- Werkervaringen (op bedrijf+periode) ---
        var werkKeys = db.Werkervaringen.Select(w => w.Bedrijf + "|" + w.Periode).ToHashSet();
        foreach (var w in data.Werkervaringen)
        {
            var key = w.Bedrijf + "|" + w.Periode;
            if (werkKeys.Contains(key)) continue;
            db.Werkervaringen.Add(new Werkervaring
            {
                Periode = w.Periode,
                Bedrijf = w.Bedrijf,
                Functie = w.Functie,
                Locatie = w.Locatie,
                Omschrijving = w.Omschrijving,
                IsGepubliceerd = w.IsGepubliceerd,
                Volgorde = w.Volgorde
            });
            werkKeys.Add(key);
        }

        // --- POV-locaties (op slug) ---
        var povBySlug = new Dictionary<string, int>(db.PovLocaties.ToDictionary(l => l.Slug, l => l.Id));
        foreach (var l in data.Pov)
        {
            if (povBySlug.ContainsKey(l.Slug)) continue;
            var locatie = new PovLocatie
            {
                Naam = l.Naam,
                Slug = l.Slug,
                Bedrijf = l.Bedrijf,
                Adres = l.Adres,
                Periode = l.Periode,
                Omschrijving = l.Omschrijving,
                IsGepubliceerd = l.IsGepubliceerd,
                Volgorde = l.Volgorde,
                Schooljaar = l.Schooljaar
            };
            db.PovLocaties.Add(locatie);
        }

        // --- Oude hardcoded stage — alleen bij een leeg Stages-tabel ---
        if (!db.Stages.Any())
        {
            db.Stages.Add(new Stage
            {
                Periode = "2026 · april — juni",
                Bedrijf = "Elektro Vanstapel",
                Functie = "Stagiair elektricien",
                Locatie = "Roeselare",
                Omschrijving = "Stageperiode bij een elektrobedrijf dat residentiële en kleine professionele installaties aanlegt en onderhoudt.",
                Taken = "Meewerken aan de opbouw en bedrading van verdeelborden\nInstallaties aanbrengen en meterstanden opnemen\nControle van aarding en beveiliging vóór de oplevering\nMeewerken aan keuringsrapporten en de voorbereiding van keuringen",
                IsGepubliceerd = true,
                Volgorde = 1
            });
        }

        // --- Pagina-inhouden (op sleutel) ---
        var inhoudKeys = db.PaginaInhouden.Select(i => i.Sleutel).ToHashSet();
        foreach (var i in data.PaginaInhouden)
        {
            if (inhoudKeys.Contains(i.Sleutel)) continue;
            db.PaginaInhouden.Add(new PaginaInhoud
            {
                Sleutel = i.Sleutel,
                Inhoud = i.Inhoud,
                IsGepubliceerd = true
            });
            inhoudKeys.Add(i.Sleutel);
        }

        // Fase 1 opslaan: nieuwe projecten/pov's krijgen hun echte Id.
        db.SaveChanges();

        // Echte Id's van (mogelijk net nieuwe) POV-locaties ophalen voor de rapportkoppeling.
        povBySlug = db.PovLocaties.ToDictionary(l => l.Slug, l => l.Id);

        // --- Galerijafbeeldingen (op project + pad), met echte Project-Ids ---
        var padenPerProject = db.GallerijAfbeeldingen
            .Select(g => new { g.ProjectId, g.Pad })
            .ToList()
            .GroupBy(g => g.ProjectId)
            .ToDictionary(t => t.Key, t => new HashSet<string>(t.Select(x => x.Pad)));

        foreach (var g in data.Gallerij)
        {
            if (g.ProjectSlug is null || !projectenBySlug.TryGetValue(g.ProjectSlug, out var project))
            {
                continue;
            }

            if (!padenPerProject.TryGetValue(project.Id, out var paden))
            {
                paden = new HashSet<string>();
                padenPerProject[project.Id] = paden;
            }
            if (paden.Contains(g.Pad)) continue;

            db.GallerijAfbeeldingen.Add(new GallerijAfbeelding
            {
                ProjectId = project.Id,
                Pad = g.Pad,
                Onderschrift = g.Onderschrift,
                Soort = g.Soort,
                IsHoofdCover = g.IsHoofdCover,
                Volgorde = g.Volgorde
            });
            paden.Add(g.Pad);
        }

        // --- Rapporten (op pad) — gekoppeld aan pov-slug of een bestaande stage ---
        var rapportPaden = db.Rapporten.Select(r => r.Pad).ToHashSet();
        foreach (var r in data.Rapporten)
        {
            if (rapportPaden.Contains(r.Pad)) continue;

            int? povId = r.PovSlug is not null && povBySlug.TryGetValue(r.PovSlug, out var pid)
                ? pid
                : null;

            db.Rapporten.Add(new Rapport
            {
                StageId = null,
                PovLocatieId = povId,
                Pad = r.Pad,
                Titel = r.Titel,
                Datum = ParseDatum(r.Datum),
                IsGepubliceerd = r.IsGepubliceerd,
                Volgorde = r.Volgorde
            });
            rapportPaden.Add(r.Pad);
        }

        // Oude stage-verslag-PDF omzetten naar een Rapport-record, zodat
        // oudere uploads behouden blijven en in de dagrapport-lijst verschijnen.
        foreach (var stage in db.Stages.Include(s => s.Rapporten).Where(s => s.VerslagPad != null).ToList())
        {
            if (stage.Rapporten.Any(r => r.Pad == stage.VerslagPad)) continue;
            db.Rapporten.Add(new Rapport
            {
                StageId = stage.Id,
                Pad = stage.VerslagPad!,
                Titel = "Stageverslag",
                Datum = DateTime.Today,
                IsGepubliceerd = true,
                Volgorde = 1
            });
        }

        // Fase 2 opslaan: galerij + rapporten (alleen nieuwe rijen).
        db.SaveChanges();
    }

    private static DateTime ParseDatum(string? v)
        => DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.Today;

    private static void Log(IServiceProvider services, LogLevel niveau, Exception? ex, string melding)
    {
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger("DataInitializer");
        if (ex is null) logger?.Log(niveau, melding);
        else logger?.Log(niveau, ex, melding);
    }

    // ---------- Inleesmodellen voor StandaardData.Json ----------

    private sealed class StandaardDataset
    {
        public ProfielJson? Profiel { get; set; }
        public List<ProjectJson> Projecten { get; set; } = new();
        public List<GallerijJson> Gallerij { get; set; } = new();
        public List<OpleidingJson> Opleidingen { get; set; } = new();
        public List<WerkervaringJson> Werkervaringen { get; set; } = new();
        public List<PovJson> Pov { get; set; } = new();
        public List<RapportJson> Rapporten { get; set; } = new();
        public List<PaginaInhoudJson> PaginaInhouden { get; set; } = new();
    }

    private sealed class ProfielJson
    {
        public string VolledigeNaam { get; set; } = "";
        public string? Tagline { get; set; }
        public string KorteIntroductie { get; set; } = "";
        public string OverMijTekst { get; set; } = "";
        public string? Woonplaats { get; set; }
        public string? Email { get; set; }
        public string? Telefoon { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? ProfielfotoPad { get; set; }
    }

    private sealed class ProjectJson
    {
        public string Titel { get; set; } = "";
        public string? Slug { get; set; }
        public string? Categorie { get; set; }
        public string? Periode { get; set; }
        public string? Rol { get; set; }
        public string? Technologieen { get; set; }
        public string KorteSamenvatting { get; set; } = "";
        public string Omschrijving { get; set; } = "";
        public string? Uitdaging { get; set; }
        public string? Geleerd { get; set; }
        public string? CoverPad { get; set; }
        public bool IsUitgelicht { get; set; }
        public bool IsGepubliceerd { get; set; }
        public int Volgorde { get; set; }
        public string? Schooljaar { get; set; }
        public string? Vak { get; set; }
    }

    private sealed class GallerijJson
    {
        public string? ProjectSlug { get; set; }
        public string Pad { get; set; } = "";
        public string? Onderschrift { get; set; }
        public int Volgorde { get; set; }
        public bool IsHoofdCover { get; set; }
        public string Soort { get; set; } = "afbeelding";
    }

    private sealed class OpleidingJson
    {
        public string Periode { get; set; } = "";
        public string Titel { get; set; } = "";
        public string? Instelling { get; set; }
        public string? Locatie { get; set; }
        public string Omschrijving { get; set; } = "";
        public bool IsGepubliceerd { get; set; }
        public int Volgorde { get; set; }
    }

    private sealed class WerkervaringJson
    {
        public string Periode { get; set; } = "";
        public string Bedrijf { get; set; } = "";
        public string? Functie { get; set; }
        public string? Locatie { get; set; }
        public string Omschrijving { get; set; } = "";
        public bool IsGepubliceerd { get; set; }
        public int Volgorde { get; set; }
    }

    private sealed class PovJson
    {
        public string Naam { get; set; } = "";
        public string Slug { get; set; } = "";
        public string? Bedrijf { get; set; }
        public string? Adres { get; set; }
        public string? Periode { get; set; }
        public string Omschrijving { get; set; } = "";
        public bool IsGepubliceerd { get; set; }
        public int Volgorde { get; set; }
        public string? Schooljaar { get; set; }
    }

    private sealed class RapportJson
    {
        public string Pad { get; set; } = "";
        public string? Titel { get; set; }
        public string? Datum { get; set; }
        public bool IsGepubliceerd { get; set; }
        public int Volgorde { get; set; }
        public string? PovSlug { get; set; }
        public string? StageBedrijf { get; set; }
    }

    private sealed class PaginaInhoudJson
    {
        public string Sleutel { get; set; } = "";
        public string Inhoud { get; set; } = "";
    }
}
