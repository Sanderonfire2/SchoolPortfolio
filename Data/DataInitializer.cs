using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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

        // 1. Schema: EnsureCreated (nieuwe databases) + migratiegeschiedenis
        //    + veilige aanmaak van recente tabellen + Migrate voor oudere databases.
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
            VulMigratiegeschiedenisAan(db);
        }
        catch (Exception ex)
        {
            Log(services, LogLevel.Error, ex, "Migratiegeschiedenis kan niet worden aangevuld.");
        }

        // Oudere EnsureCreated-databases missen de nieuwste tabellen; die worden hier
        // expliciet bijgemaakt zodat Migrate() niet op een halffabriek-strand oord Eindigt.
        try
        {
            MaakOntbrekendeTabellenAan(db);

            // De migratie die deze tabellen zou aanmaken is nu inhoudelijk uitgevoerd.
            // Zonder deze stempel blijft ze 'pending' en blokkeert ze élke latere
            // migratie: Migrate() breekt af op de al bestaande tabel en de migratie
            // die daarna kwam (de kolom Beantwoord) wordt dan nooit toegepast.
            StempelMigratieAlsToegepast(db, "20261027094014_VoegFaqDownloadsEnAreiToe");
        }
        catch (Exception ex)
        {
            Log(services, LogLevel.Error, ex, "Ontbrekende tabellen kunnen niet worden aangemaakt.");
        }

        try
        {
            // Migrate() kan falen op databases waar EnsureCreated al-tabellen
            // heeft aangemaakt die ook in een latere migratie voorkomen.
            // We laten het trachten, en als het faalt door een 'reeds bestaat'-fout,
            // dan gaan we ervan uit dat de tabellen al correct zijn.
            db.Database.Migrate();
        }
        catch (Exception ex) when (IsReedsBestaandFout(ex))
        {
            Log(services, LogLevel.Warning, ex,
                "Migrate() is gefaald omdat een tabel/content al bestaat — " +
                "dat is OK als de tabel al correct is (EnsureCreated of handmatig).");
        }
        catch (Exception ex)
        {
            Log(services, LogLevel.Error, ex, "Migrate() mislukt.");
        }

        // Vangnet: voeg kolommen toe die door een overgeslagen of mislukte migratie
        // ontbreken. Zo blijft een oudere database bruikbaar, ook wanneer de
        // migratiegeschiedenis niet (meer) klopt met het echte schema.
        try
        {
            HerstelOntbrekendeKolommen(db, services);
        }
        catch (Exception ex)
        {
            Log(services, LogLevel.Error, ex, "Ontbrekende kolommen kunnen niet worden hersteld.");
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

    /// <summary>
    /// Databases die ooit via EnsureCreated zijn opgebouwd hebben geen
    /// __EFMigrationsHistory-tabel; zonder deze stempel probeert Migrate() alle
    /// migraties opnieuw en crasht het op reeds bestaande kolommen/tabellen.
    /// De stempel zet alle al-bestaande migraties als toegepast, zodat alleen
    /// écht nieuwe migraties nog worden uitgevoerd.
    /// </summary>
    private static void VulMigratiegeschiedenisAan(SiteDbContext db)
    {
        var connectie = (Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection();
        var wasOpen = connectie.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            connectie.Open();
        }

        try
        {
            using var opdracht = connectie.CreateCommand();

            opdracht.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'";
            var bestaatAl = Convert.ToInt64(opdracht.ExecuteScalar()) > 0;
            if (!bestaatAl)
            {
                opdracht.CommandText =
                    """CREATE TABLE "__EFMigrationsHistory" ("MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY, "ProductVersion" TEXT NOT NULL);""";
                opdracht.ExecuteNonQuery();

                opdracht.CommandText =
                    """INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES (@nr, @v);""";
                var nr = opdracht.Parameters.Add("@nr", Microsoft.Data.Sqlite.SqliteType.Text);
                var v = opdracht.Parameters.Add("@v", Microsoft.Data.Sqlite.SqliteType.Text);
                var versie = typeof(SiteDbContext).Assembly.GetName().Version!.ToString(3);
                foreach (var migratie in db.Database.GetMigrations())
                {
                    nr.Value = migratie;
                    v.Value = versie;
                    opdracht.ExecuteNonQuery();
                }
            }
        }
        finally
        {
            if (!wasOpen && connectie.State == System.Data.ConnectionState.Open)
            {
                connectie.Close();
            }
        }
    }

    /// <summary>
    /// Maakt tabellen van recente migraties expliciet aan op oudere
    /// EnsureCreated-databases (iets dat de stempel-registratie overslaat).
    /// </summary>
    private static void MaakOntbrekendeTabellenAan(SiteDbContext db)
    {
        var scripts = new[]
        {
            """
            CREATE TABLE IF NOT EXISTS "FaqItems" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_FaqItems" PRIMARY KEY AUTOINCREMENT,
                "Vraag" TEXT NOT NULL,
                "Antwoord" TEXT NOT NULL,
                "IsGepubliceerd" INTEGER NOT NULL,
                "Volgorde" INTEGER NOT NULL);
            """,
            """
            CREATE TABLE IF NOT EXISTS "DownloadItems" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_DownloadItems" PRIMARY KEY AUTOINCREMENT,
                "Titel" TEXT NOT NULL,
                "Omschrijving" TEXT NULL,
                "BestandPad" TEXT NOT NULL,
                "IsGepubliceerd" INTEGER NOT NULL,
                "Volgorde" INTEGER NOT NULL);
            """,
            """
            CREATE TABLE IF NOT EXISTS "AreiBoeken" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_AreiBoeken" PRIMARY KEY AUTOINCREMENT,
                "Naam" TEXT NOT NULL,
                "Slug" TEXT NOT NULL,
                "Beschrijving" TEXT NULL,
                "BestandPad" TEXT NULL,
                "Volgorde" INTEGER NOT NULL);
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_AreiBoeken_Slug" ON "AreiBoeken" ("Slug");
            """
        };

        foreach (var script in scripts)
        {
            db.Database.ExecuteSqlRaw(script);
        }
    }

    /// <summary>
    /// Zet een migratie op 'toegepast' in de geschiedenis, zonder iets aan het
    /// schema te veranderen. Alleen gebruiken wanneer de inhoud van die migratie
    /// al op een andere manier is uitgevoerd (zoals de tabellen hierboven).
    /// </summary>
    private static void StempelMigratieAlsToegepast(SiteDbContext db, string migratieId)
    {
        var versie = typeof(SiteDbContext).Assembly.GetName().Version!.ToString(3);
        db.Database.ExecuteSqlRaw(
            "INSERT OR IGNORE INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({0}, {1});",
            migratieId, versie);
    }

    /// <summary>
    /// Vergelijkt het echte SQLite-schema met het EF-model en voegt elke kolom toe
    /// die ontbreekt. Dit repareert databases waarvan de migratiegeschiedenis is
    /// gestempeld (waardoor Migrate() niets meer doet) of waarvan een migratie is
    /// afgebroken op een al bestaande tabel. Bestaande gegevens blijven staan.
    /// </summary>
    private static void HerstelOntbrekendeKolommen(SiteDbContext db, IServiceProvider services)
    {
        var connectie = (SqliteConnection)db.Database.GetDbConnection();
        var wasOpen = connectie.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            connectie.Open();
        }

        try
        {
            var toegevoegd = new List<string>();

            foreach (var entiteit in db.Model.GetEntityTypes())
            {
                var tabel = entiteit.GetTableName();
                if (string.IsNullOrWhiteSpace(tabel))
                {
                    continue;
                }

                var bestaandeKolommen = KolomNamen(connectie, tabel);
                if (bestaandeKolommen.Count == 0)
                {
                    // Tabel bestaat (nog) niet; die komt uit een migratie of EnsureCreated.
                    continue;
                }

                var primaireSleutel = entiteit.FindPrimaryKey()?.Properties;

                foreach (var eigenschap in entiteit.GetProperties())
                {
                    if (primaireSleutel is not null && primaireSleutel.Contains(eigenschap))
                    {
                        continue;
                    }

                    var kolom = eigenschap.GetColumnName();
                    if (string.IsNullOrWhiteSpace(kolom) || bestaandeKolommen.Contains(kolom))
                    {
                        continue;
                    }

                    var sqlType = eigenschap.GetColumnType();
                    string? standaard = null;
                    if (!eigenschap.IsNullable)
                    {
                        standaard = StandaardWaarde(eigenschap);
                        if (standaard is null)
                        {
                            Log(services, LogLevel.Warning, null,
                                "Kolom " + tabel + "." + kolom + " ontbreekt en heeft geen veilige standaardwaarde; " +
                                "voeg hem handmatig toe.");
                            continue;
                        }
                    }

                    try
                    {
                        using var opdracht = connectie.CreateCommand();
                        opdracht.CommandText =
                            "ALTER TABLE \"" + tabel.Replace("\"", "\"\"") + "\" ADD COLUMN \"" +
                            kolom.Replace("\"", "\"\"") + "\" " + sqlType +
                            (standaard is null ? string.Empty : " NOT NULL DEFAULT " + standaard) + ";";
                        opdracht.ExecuteNonQuery();

                        toegevoegd.Add(tabel + "." + kolom);
                    }
                    catch (Exception ex)
                    {
                        // Eén kolom mag de rest van de reparatie niet blokkeren.
                        Log(services, LogLevel.Warning, ex,
                            "Kolom " + tabel + "." + kolom + " kon niet worden toegevoegd.");
                    }
                }
            }

            if (toegevoegd.Count > 0)
            {
                Log(services, LogLevel.Warning, null,
                    "Ontbrekende kolommen in de database toegevoegd: " + string.Join(", ", toegevoegd) + ".");
            }
        }
        finally
        {
            if (!wasOpen && connectie.State == System.Data.ConnectionState.Open)
            {
                connectie.Close();
            }
        }
    }

    private static HashSet<string> KolomNamen(SqliteConnection connectie, string tabel)
    {
        using var opdracht = connectie.CreateCommand();
        opdracht.CommandText = "PRAGMA table_info(\"" + tabel.Replace("\"", "\"\"") + "\");";

        using var lezer = opdracht.ExecuteReader();
        var namen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (lezer.Read())
        {
            namen.Add(lezer.GetString(1));
        }

        return namen;
    }

    /// <summary>
    /// Geeft een SQL-standaardwaarde voor een verplichte kolom, afgeleid van het
    /// CLR-type (of van de expliciete standaardwaarde in het model).
    /// </summary>
    private static string? StandaardWaarde(IProperty eigenschap)
    {
        if (eigenschap.GetDefaultValue() is { } ingesteld)
        {
            return ingesteld switch
            {
                bool b => b ? "1" : "0",
                string s => "'" + s.Replace("'", "''") + "'",
                DateTime d => "'" + d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'",
                _ => Convert.ToString(ingesteld, CultureInfo.InvariantCulture)
            };
        }

        var standaardSql = eigenschap.GetDefaultValueSql();
        if (!string.IsNullOrWhiteSpace(standaardSql))
        {
            return standaardSql;
        }

        var type = Nullable.GetUnderlyingType(eigenschap.ClrType) ?? eigenschap.ClrType;

        if (type == typeof(bool) || type == typeof(byte) || type == typeof(short) ||
            type == typeof(int) || type == typeof(long) || type == typeof(float) ||
            type == typeof(double) || type == typeof(decimal) || type.IsEnum)
        {
            return "0";
        }

        if (type == typeof(string))
        {
            return "''";
        }

        if (type == typeof(DateTime))
        {
            return "'0001-01-01 00:00:00'";
        }

        if (type == typeof(DateTimeOffset))
        {
            return "'0001-01-01 00:00:00+00:00'";
        }

        if (type == typeof(TimeSpan))
        {
            return "'00:00:00'";
        }

        if (type == typeof(Guid))
        {
            return "'00000000-0000-0000-0000-000000000000'";
        }

        if (type == typeof(byte[]))
        {
            return "X''";
        }

        return null;
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

        // --- Starter-FAQ: alleen bij een lege FaqItems-tabel, zodat admin-wijzigingen veilig zijn ---
        if (!db.FaqItems.Any())
        {
            var starterVragen = new (string Vraag, string Antwoord)[]
            {
                ("Wie is Sander van Praet?",
                 "Sander van Praet is student elektriciteit. Op deze site vind je zijn schoolcarrière, projecten, stageverslagen en POV-dagrapporten."),
                ("Wat kun je op deze site vinden?",
                 "Projecten per schooljaar en vak, stage-ervaring, POV-locaties met dagrapporten, opleidingen, werkervaring, de AREI-reglementen en extra downloads."),
                ("Wat is de AREI?",
                 "Het Algemeen Reglement op de Elektrische Installaties (AREI) is het Belgische veiligheidsreglement voor elektrische installaties. Boek 1 geldt voor laagspanning en zeer lage spanning, Boek 2 voor hoogspanning en Boek 3 voor transmissie en distributie."),
                ("Kan ik documenten downloaden?",
                 "Ja, op de downloads-pagina en bij de AREI-boeken kun je de pdf's openen in de browser of opslaan."),
                ("Hoe kan ik contact opnemen?",
                 "Via het contactformulier op de contactpagina of via de e-mail in de voettekst. Berichten komen rechtstreeks in het beheerdersgedeelte terecht.")
            };
            var faqVolgorde = 1;
            foreach (var (vraag, antwoord) in starterVragen)
            {
                db.FaqItems.Add(new FaqItem
                {
                    Vraag = vraag,
                    Antwoord = antwoord,
                    IsGepubliceerd = true,
                    Volgorde = faqVolgorde++
                });
            }
        }

        // --- AREI-boeken (op slug); BestandPad verwijst statisch naar wwwroot/downloaden/arei ---
        var areiSlugs = db.AreiBoeken.Select(b => b.Slug).ToHashSet();
        var areiStandaard = new (string Naam, string Slug, string Pad, string Beschrijving)[]
        {
            ("Boek 1 · Installaties op laagspanning en op zeer lage spanning",
             "boek-1-laagspanning",
             "/downloaden/arei/boek-1-installaties-op-laagspanning-en-op-zeer-lage-spanning.pdf",
             "Installaties op laagspanning en op zeer lage spanning — editie 2026."),
            ("Boek 2 · Installaties op hoogspanning",
             "boek-2-hoogspanning",
             "/downloaden/arei/boek-2-installaties-op-hoogspanning.pdf",
             "Installaties op hoogspanning — editie 2026."),
            ("Boek 3 · Installaties voor transmissie en distributie van elektrische energie",
             "boek-3-transmissie-en-distributie",
             "/downloaden/arei/boek-3-installaties-voor-transmissie-en-distributie-van-elektrische-energie.pdf",
             "Installaties voor transmissie en distributie van elektrische energie — editie 2026.")
        };
        for (var i = 0; i < areiStandaard.Length; i++)
        {
            var (naam, slug, pad, beschrijving) = areiStandaard[i];
            if (areiSlugs.Contains(slug)) continue;
            db.AreiBoeken.Add(new AreiBoek
            {
                Naam = naam,
                Slug = slug,
                Beschrijving = beschrijving,
                BestandPad = pad,
                Volgorde = i + 1
            });
            areiSlugs.Add(slug);
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

    private static bool IsReedsBestaandFout(Exception ex)
    {
        if (ex is SqliteException sq && sq.SqliteErrorCode == 1)
        {
            // 'has no column named' hoort hier níet bij: dat betekent dat het schema
            // achterloopt en moet worden gerepareerd, niet dat de migratie klaar is.
            return sq.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
                   sq.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase);
        }
        return false;
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
