using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SchoolPortfolio.Data;
using SchoolPortfolio.Models;
using SchoolPortfolio.Services;

namespace SchoolPortfolio.Controllers;

[Authorize]
[Route("admin")]
public class AdminController(SiteDbContext db, IConfiguration configuratie, UploadService uploads, EmailService email, ILogger<AdminController> logger, GmailGeheimen gmailGeheimen) : Controller
{
    private string? Wachtwoord => LeesWachtwoord(configuratie);

    /// <summary>
    /// Leest het beheerderswachtwoord uit de omgeving of de configuratie.
    /// Geeft null terug als er niets ingesteld is: de login toont dan een duidelijke
    /// melding in plaats van een uitzondering (vroeger gaf dit op de productieserver
    /// bij elk wachtwoord een foutpagina).
    /// </summary>
    private static string? LeesWachtwoord(IConfiguration configuratie)
    {
        // Prioriteit: 1) expliciete omgevingsvariabele, 2) appsettings.
        var viaEnv = configuratie["Beheer__Wachtwoord"];
        if (!string.IsNullOrWhiteSpace(viaEnv))
        {
            return viaEnv.Trim();
        }

        var viaConfig = configuratie["Beheer:Wachtwoord"];
        return string.IsNullOrWhiteSpace(viaConfig) ? null : viaConfig.Trim();
    }

    /// <summary>
    /// Constant-time vergelijking: lekt geen timinginformatie over de lengte/hoeveelheid
    /// overeenkomsten van het echte wachtwoord.
    /// </summary>
    private static bool KomtOvereen(string ingevoerd, string opgeslagen)
    {
        // Leeg ingevoerd wachtwoord is nooit geldig.
        if (string.IsNullOrWhiteSpace(ingevoerd))
        {
            return false;
        }

        var hashIngevoerd = SHA256.HashData(Encoding.UTF8.GetBytes(ingevoerd));
        var hashOpgeslagen = SHA256.HashData(Encoding.UTF8.GetBytes(opgeslagen));
        return CryptographicOperations.FixedTimeEquals(hashIngevoerd, hashOpgeslagen);
    }

    /* ---------- Inloggen en uitloggen ---------- */

    [AllowAnonymous]
    [HttpGet("inloggen")]
    public IActionResult Inloggen(string? terug)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Dashboard));
        }
        ViewData["Titel"] = "Beheer · inloggen";
        ViewBag.TerugUrl = terug;
        ViewBag.WachtwoordNietIngesteld = Wachtwoord is null;
        return View();
    }

    [AllowAnonymous]
    [HttpPost("inloggen")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("inloggen")]
    public async Task<IActionResult> Inloggen(string wachtwoord, string? terug)
    {
        var ingesteld = Wachtwoord;
        if (ingesteld is null)
        {
            logger.LogError("Er is geen beheerderswachtwoord geconfigureerd; inloggen is geblokkeerd. " +
                "Stel de omgevingsvariabele Beheer__Wachtwoord in (of Beheer:Wachtwoord in appsettings) en herstart de site.");
            ModelState.AddModelError(string.Empty,
                "Op deze server is nog geen beheerderswachtwoord ingesteld. Stel de omgevingsvariabele " +
                "Beheer__Wachtwoord in (of Beheer:Wachtwoord in appsettings.json) en herstart de site.");
            ViewBag.TerugUrl = terug;
            ViewBag.WachtwoordNietIngesteld = true;
            return View();
        }

        if (string.IsNullOrWhiteSpace(wachtwoord) || !KomtOvereen(wachtwoord, ingesteld))
        {
            ModelState.AddModelError(string.Empty, "Onjuist wachtwoord. Probeer opnieuw.");
            ViewBag.TerugUrl = terug;
            return View();
        }

        var identiteit = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, string.Empty);
        identiteit.AddClaim(new Claim(ClaimTypes.Name, "Beheerder"));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identiteit),
            new AuthenticationProperties { IsPersistent = true });

        var veiligeTerug = Url.IsLocalUrl(terug) ? terug : Url.Action(nameof(Dashboard))!;
        return Redirect(veiligeTerug);
    }

    [HttpPost("uitloggen")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Uitloggen()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }

    /* ---------- Overzicht ---------- */

    [HttpGet("")]
    public IActionResult Dashboard()
    {
        ViewData["Titel"] = "Overzicht";
        var model = new DashboardWeergave
        {
            AantalProjecten = db.Projecten.Count(),
            AantalOngelezenBerichten = db.ContactBerichten.Count(b => !b.IsGelezen),
            AantalOpleidingen = db.Opleidingen.Count(),
            AantalWerkervaringen = db.Werkervaringen.Count(),
            AantalPovLocaties = db.PovLocaties.Count(),
            LaatsteBerichten = db.ContactBerichten
                .OrderByDescending(b => b.VerstuurdOp)
                .Take(5)
                .ToList()
        };
        return View(model);
    }

    /* ---------- Berichten ---------- */

    [HttpGet("berichten")]
    public IActionResult Berichten()
    {
        ViewData["Titel"] = "Berichten";
        var berichten = db.ContactBerichten.OrderByDescending(b => b.VerstuurdOp).ToList();
        return View(berichten);
    }

    /* ---------- E-mail ---------- */

    [HttpGet("email")]
    public IActionResult Email()
    {
        ViewData["Titel"] = "E-mail";
        var profiel = db.Profielen.FirstOrDefault();
        var adres = profiel?.Email?.Trim();
        var voorstelling = profiel?.VolledigeNaam?.Trim() ?? "Sander Van Praet";

        var (gmailAdres, _) = gmailGeheimen.Lezen();

        return View(new EmailWeergave
        {
            Adres = adres ?? "(nog niet ingesteld)",
            Voorstelling = voorstelling,
            GmailAdres = gmailAdres ?? "(nog niet gekoppeld)",
            GmailIsGekoppeld = gmailGeheimen.IsGekoppeld,
            SmtpServer = "smtp.gmail.com",
            SmtpPoort = "587",
            SmtpGesteld = gmailGeheimen.IsGekoppeld,
            SmtpGebruiker = gmailAdres ?? ""
        });
    }

    [HttpPost("email")]
    [ValidateAntiForgeryToken]
    public IActionResult GmailOpslaan(string gmailAdres, string gmailAppWachtwoord)
    {
        ViewData["Titel"] = "E-mail";

        if (string.IsNullOrWhiteSpace(gmailAdres) || !gmailAdres.Contains('@'))
        {
            TempData["Foutmelding"] = "Voer een geldig Gmail-adres in (bijvoorbeeld naam@gmail.com).";
            return RedirectToAction(nameof(Email));
        }

        // Bij het wijzigen van alleen het adres mag het bestaande App-wachtwoord blijven staan.
        var bestaandWachtwoord = gmailGeheimen.Lezen().AppWachtwoord;
        if (string.IsNullOrWhiteSpace(gmailAppWachtwoord))
        {
            if (string.IsNullOrWhiteSpace(bestaandWachtwoord))
            {
                TempData["Foutmelding"] = "Vul ook het Google App-wachtwoord in (16 tekens). Zonder App-wachtwoord kan er geen mail verzonden worden.";
                return RedirectToAction(nameof(Email));
            }

            gmailAppWachtwoord = bestaandWachtwoord;
        }

        try
        {
            gmailGeheimen.Opslaan(gmailAdres.Trim(), gmailAppWachtwoord);
            logger.LogInformation("Gmail-adres gekoppeld: {Adres}.", gmailAdres);
            TempData["Melding"] = "Gmail-adres en App-wachtwoord zijn opgeslagen. Sla eventueel je profiel-opzenderadres op in /admin/profiel.";

            // Koppel ook het profiel als nog niet ingesteld, zodat de afzender consistent is.
            var profiel = db.Profielen.FirstOrDefault();
            if (profiel is null)
            {
                db.Profielen.Add(new Profiel
                {
                    VolledigeNaam = "Sander Van Praet",
                    Email = gmailAdres.Trim(),
                    BijgewerktOp = DateTime.Now
                });
                db.SaveChanges();
            }
            else if (string.IsNullOrWhiteSpace(profiel.Email))
            {
                profiel.Email = gmailAdres.Trim();
                profiel.BijgewerktOp = DateTime.Now;
                db.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gmail-instelling kon niet worden opgeslagen.");
            TempData["Foutmelding"] = "De Gmail-instelling kon niet worden opgeslagen. Zie de serverlog voor details.";
        }

        return RedirectToAction(nameof(Email));
    }

    [HttpPost("email/verwijderen")]
    [ValidateAntiForgeryToken]
    public IActionResult GmailVerwijderen()
    {
        ViewData["Titel"] = "E-mail";

        try
        {
            gmailGeheimen.Verwijderen();
            logger.LogInformation("Gmail-adres en App-wachtwoord zijn verwijderd.");
            TempData["Melding"] = "Gmail-adres en App-wachtwoord zijn verwijderd.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gmail-instelling kon niet worden verwijderd.");
            TempData["Foutmelding"] = "De Gmail-instelling kon niet worden verwijderd. Zie de serverlog voor details.";
        }

        return RedirectToAction(nameof(Email));
    }

    [HttpPost("email/test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailTest()
    {
        var profiel = db.Profielen.FirstOrDefault();
        var adres = profiel?.Email?.Trim();
        ViewData["Titel"] = "E-mail";

        if (string.IsNullOrWhiteSpace(adres))
        {
            TempData["Foutmelding"] = "Stel eerst een e-mailadres in op /admin/profiel voordat je de testmail kunt verzenden.";
            return RedirectToAction(nameof(Email));
        }

        if (!gmailGeheimen.IsGekoppeld)
        {
            TempData["Foutmelding"] = "Er is geen Gmail-adres gekoppeld. Koppel eerst een Gmail-adres via het formulier hierboven.";
            return RedirectToAction(nameof(Email));
        }

        try
        {
            await email.VerstuurTestmailAsync(adres!);

            TempData["Melding"] = $"Testmail verzonden naar {adres}. Controleer je inbox (en eventueel de spammap).";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Testmail kon niet worden verzonden.");

            // Toon de leesbare oorzaak én de ruwe melding van de Gmail-server,
            // zodat een verkeerd App-wachtwoord of een geblokkeerde verbinding
            // meteen duidelijk is.
            var fouttekst = ex.Message;
            if (ex.InnerException is not null &&
                !fouttekst.Contains(ex.InnerException.Message, StringComparison.OrdinalIgnoreCase))
            {
                fouttekst += $" (Gmail-server: {ex.InnerException.Message})";
            }

            fouttekst = fouttekst.Replace("\r", " ").Replace("\n", " ");
            TempData["Foutmelding"] = $"Testmail kon niet worden verzonden: {fouttekst}";
        }

        return RedirectToAction(nameof(Email));
    }

    /* ---------- Berichten ---------- */

    [HttpPost("berichten/{id:int}/gelezen")]
    [ValidateAntiForgeryToken]
    public IActionResult BerichtenGelezen(int id)
    {
        var bericht = db.ContactBerichten.Find(id);
        if (bericht is not null)
        {
            bericht.IsGelezen = true;
            db.SaveChanges();
        }
        return RedirectToAction(nameof(Berichten));
    }

    [HttpPost("berichten/{id:int}/verwijderen")]
    [ValidateAntiForgeryToken]
    public IActionResult BerichtenVerwijderen(int id)
    {
        var bericht = db.ContactBerichten.Find(id);
        if (bericht is not null)
        {
            db.ContactBerichten.Remove(bericht);
            db.SaveChanges();
        }
        return RedirectToAction(nameof(Berichten));
    }

    /* ---------- Projecten ---------- */

    [HttpGet("projecten")]
    public IActionResult ProjectenLijst()
    {
        ViewData["Titel"] = "Projecten beheren";
        var projecten = db.Projecten
            .OrderBy(p => p.Schooljaar)
            .ThenBy(p => p.Vak)
            .ThenBy(p => p.Volgorde)
            .ToList();
        return View("Projecten", projecten);
    }

    [HttpGet("projecten/nieuw")]
    public IActionResult ProjectNieuw()
    {
        ViewData["Titel"] = "Nieuw project";
        ViewBag.IsNieuw = true;
        return View("ProjectFormulier", new Project());
    }

    [HttpPost("projecten/nieuw")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(80 * 1024 * 1024)]
    public async Task<IActionResult> ProjectNieuw(Project formulier, IFormFile? coverBestand, List<IFormFile>? gallerijBestanden, string? gallerijBijschriften)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.IsNieuw = true;
            ViewBag.Bijschriften = gallerijBijschriften;
            return View("ProjectFormulier", formulier);
        }

        try
        {
            var cover = await uploads.SlaOpAsync(coverBestand, "cover");
            if (cover is not null) formulier.CoverPad = cover;

            formulier.Slug = ZetSlug(formulier.Slug, formulier.Titel);
            formulier.BijgewerktOp = DateTime.Now;
            formulier.IsGepubliceerd = formulier.IsGepubliceerd; // keuze van de gebruiker eerbiedigen
            await VulGallerijUitUploadsAsync(formulier, gallerijBestanden, gallerijBijschriften);

            formulier.Volgorde = db.Projecten.Count() + 1;

            db.Projecten.Add(formulier);
            await db.SaveChangesAsync();

            // Cover: eerste gallerijafbeelding wordt de cover als er geen aparte cover-upload is.
            var eersteCover = formulier.Afbeeldingen
                .OrderBy(a => a.Volgorde)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(formulier.CoverPad) && eersteCover is not null)
            {
                eersteCover.IsHoofdCover = true;
                formulier.CoverPad = eersteCover.Pad;
                await db.SaveChangesAsync();
            }

            TempData["Melding"] = $"Project “{formulier.Titel}” opgeslagen. Kies hieronder de banner/cover.";
            return RedirectToAction(nameof(ProjectBewerken), new { id = formulier.Id });
        }
        catch (InvalidOperationException fout)
        {
            ModelState.AddModelError(string.Empty, fout.Message);
            ViewBag.IsNieuw = true;
            ViewBag.Bijschriften = gallerijBijschriften;
            return View("ProjectFormulier", formulier);
        }
    }

    [HttpGet("projecten/{id:int}/bewerken")]
    public async Task<IActionResult> ProjectBewerken(int id)
    {
        var project = await db.Projecten.Include(p => p.Afbeeldingen).FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return NotFound();
        ViewData["Titel"] = "Project bewerken";
        ViewBag.IsNieuw = false;
        ViewBag.BestandGallerij = project.Afbeeldingen.OrderBy(a => a.Volgorde).ToList();
        return View("ProjectFormulier", project);
    }

    [HttpPost("projecten/{id:int}/bewerken")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(80 * 1024 * 1024)]
    public async Task<IActionResult> ProjectBewerken(
        int id, Project formulier, IFormFile? coverBestand,
        List<IFormFile>? gallerijBestanden, string? gallerijBijschriften,
        List<string>? verwijderGallerij)
    {
        var project = await db.Projecten.Include(p => p.Afbeeldingen)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewBag.IsNieuw = false;
            ViewBag.BestandGallerij = project.Afbeeldingen.OrderBy(a => a.Volgorde).ToList();
            ViewBag.Bijschriften = gallerijBijschriften;
            return View("ProjectFormulier", formulier);
        }

        try
        {
            var nieuweCover = await uploads.SlaOpAsync(coverBestand, "cover");
            if (nieuweCover is not null)
            {
                // Oude coverbestand alleen verwijderen als het niet door een gallerijafbeelding gebruikt wordt:
                if (project.CoverPad is not null && !project.Afbeeldingen.Any(a => a.Pad == project.CoverPad))
                {
                    uploads.Verwijder(project.CoverPad);
                }
                project.CoverPad = nieuweCover;
                foreach (var afbeelding in project.Afbeeldingen)
                {
                    afbeelding.IsHoofdCover = false;
                }
            }

            project.Titel = formulier.Titel;
            project.Slug = ZetSlug(formulier.Slug, formulier.Titel);
            project.Schooljaar = formulier.Schooljaar;
            project.Vak = formulier.Vak;
            project.KorteSamenvatting = formulier.KorteSamenvatting;
            project.Omschrijving = formulier.Omschrijving;
            project.Uitdaging = formulier.Uitdaging;
            project.Geleerd = formulier.Geleerd;
            project.Categorie = formulier.Categorie;
            project.Periode = formulier.Periode;
            project.Rol = formulier.Rol;
            project.Technologieen = formulier.Technologieen;
            project.IsUitgelicht = formulier.IsUitgelicht;
            project.Volgorde = formulier.Volgorde;
            project.IsGepubliceerd = formulier.IsGepubliceerd;
            project.BijgewerktOp = DateTime.Now;

            // Alleen de afbeeldingen die de gebruiker expliciet met een
            // "verwijderen"-veld aanstuurt, worden opgeruimd. Alles blijft behouden.
            if (verwijderGallerij is not null && verwijderGallerij.Count > 0)
            {
                var teVerwijderenIds = verwijderGallerij
                    .Select(v => int.TryParse(v, out var g) ? g : 0)
                    .Where(g => g > 0).ToHashSet();
                var teVerwijderen = project.Afbeeldingen
                    .Where(a => teVerwijderenIds.Contains(a.Id)).ToList();
                foreach (var afbeelding in teVerwijderen)
                {
                    if (afbeelding.Pad == project.CoverPad)
                    {
                        project.CoverPad = null;
                    }
                    uploads.Verwijder(afbeelding.Pad);
                    db.GallerijAfbeeldingen.Remove(afbeelding);
                }
            }

            // Volgorde en bijschriften van bestaande afbeeldingen doorvoeren.
            var nummers = Request.Form["gallerijNummer"].ToArray();
            var volgordes = Request.Form["gallerijVolgorde"].ToArray();
            var bijschriften = Request.Form["bestaandBijschrift"].ToArray();
            foreach (var bestand in project.Afbeeldingen.ToList())
            {
                var index = Array.IndexOf(nummers, bestand.Id.ToString());
                if (index < 0) continue;
                if (index < volgordes.Length && int.TryParse(volgordes[index], out var volgorde))
                {
                    bestand.Volgorde = volgorde;
                }
                if (index < bijschriften.Length)
                {
                    bestand.Onderschrift = string.IsNullOrWhiteSpace(bijschriften[index])
                        ? null : bijschriften[index].Trim();
                }
            }

            // Nieuwe uploads toevoegen: altijd behouden, na het tussenstopje.
            var imagePath = project.Afbeeldingen.Count > 0 ? project.Afbeeldingen.Max(a => a.Volgorde) : 0;
            await VoegGallerijUploadsToeAsync(project, gallerijBestanden, gallerijBijschriften, imagePath);

            await db.SaveChangesAsync();

            // Coverkeuze: gekozen afbeelding wordt de headcover, zonder te dupliceren.
            var gekozenCover = Request.Form["hoofdCover"].FirstOrDefault();
            if (int.TryParse(gekozenCover, out var gekozenId))
            {
                var gekozenAfbeelding = project.Afbeeldingen.FirstOrDefault(a => a.Id == gekozenId);
                if (gekozenAfbeelding is not null)
                {
                    foreach (var afbeelding in project.Afbeeldingen)
                    {
                        afbeelding.IsHoofdCover = afbeelding.Id == gekozenId;
                    }
                    // BELANGRIJK: het oude coverbestand NIET verwijderen —
                    // het kan nog steeds een gallerijafbeelding zijn.
                    project.CoverPad = gekozenAfbeelding.Pad;
                }
            }
            else if (project.Afbeeldingen.Count > 0 && !project.Afbeeldingen.Any(a => a.IsHoofdCover) && string.IsNullOrWhiteSpace(project.CoverPad))
            {
                // Geen keuze en geen cover: de afbeelding met kleinste volgorde wordt de cover.
                var eerste = project.Afbeeldingen.OrderBy(a => a.Volgorde).First();
                eerste.IsHoofdCover = true;
                project.CoverPad = eerste.Pad;
            }

            await db.SaveChangesAsync();
            TempData["Melding"] = $"Project “{project.Titel}” bijgewerkt · gallerij: {project.Afbeeldingen.Count} afbeelding(en) · cover: {(string.IsNullOrEmpty(project.CoverPad) ? "—" : Path.GetFileName(project.CoverPad))}";
            return RedirectToAction(nameof(ProjectenLijst));
        }
        catch (InvalidOperationException fout)
        {
            ModelState.AddModelError(string.Empty, fout.Message);
            ViewBag.IsNieuw = false;
            ViewBag.BestandGallerij = project.Afbeeldingen.OrderBy(a => a.Volgorde).ToList();
            ViewBag.Bijschriften = gallerijBijschriften;
            return View("ProjectFormulier", formulier);
        }
    }

    [HttpPost("projecten/{id:int}/verwijderen")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProjectVerwijderen(int id)
    {
        var project = await db.Projecten.Include(p => p.Afbeeldingen)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (project is not null)
        {
            uploads.Verwijder(project.CoverPad);
            foreach (var afbeelding in project.Afbeeldingen)
            {
                uploads.Verwijder(afbeelding.Pad);
            }
            db.Projecten.Remove(project);
            await db.SaveChangesAsync();
            TempData["Melding"] = $"Project “{project.Titel}” verwijderd.";
        }
        return RedirectToAction(nameof(ProjectenLijst));
    }

    /* ---------- Profiel ---------- */

    [HttpGet("profiel")]
    public IActionResult Profiel()
    {
        ViewData["Titel"] = "Profiel aanpassen";
        var profiel = db.Profielen.FirstOrDefault();
        if (profiel is null) return NotFound();
        return View(profiel);
    }

    [HttpPost("profiel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profiel(Profiel formulier)
    {
        var profiel = db.Profielen.FirstOrDefault();
        if (profiel is null) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                var nieuweFoto = await uploads.SlaOpAsync(Request.Form.Files.GetFile("profielfotoBestand"), "profielfoto");
                if (nieuweFoto is not null)
                {
                    uploads.Verwijder(profiel.ProfielfotoPad);
                    profiel.ProfielfotoPad = nieuweFoto;
                }
            }
            catch (InvalidOperationException fout)
            {
                ModelState.AddModelError(string.Empty, fout.Message);
                formulier.Id = profiel.Id;
                return View(formulier);
            }

            profiel.VolledigeNaam = formulier.VolledigeNaam;
            profiel.Tagline = formulier.Tagline;
            profiel.KorteIntroductie = formulier.KorteIntroductie;
            profiel.OverMijTekst = formulier.OverMijTekst;
            profiel.Woonplaats = formulier.Woonplaats;
            profiel.Email = formulier.Email;
            profiel.Telefoon = formulier.Telefoon;
            profiel.LinkedInUrl = formulier.LinkedInUrl;
            profiel.BijgewerktOp = DateTime.Now;

            db.SaveChanges();
            TempData["Melding"] = "Profiel bijgewerkt.";
            return RedirectToAction(nameof(Profiel));
        }

        formulier.Id = profiel.Id;
        return View(formulier);
    }

    /* ---------- Opleidingen ---------- */

    [HttpGet("opleidingen")]
    public IActionResult Opleidingen()
    {
        ViewData["Titel"] = "Opleidingen beheren";
        ViewBag.Lijst = db.Opleidingen.OrderBy(o => o.Volgorde).ToList();
        return View("OpleidingSectie", new Opleiding());
    }

    [HttpPost("opleidingen/opslaan")]
    [ValidateAntiForgeryToken]
    public IActionResult OpleidingOpslaan(Opleiding formulier)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Lijst = db.Opleidingen.OrderBy(o => o.Volgorde).ToList();
            return View("OpleidingSectie", formulier);
        }

        if (formulier.Id == 0)
        {
            formulier.Volgorde = db.Opleidingen.Count() + 1;
            db.Opleidingen.Add(formulier);
        }
        else
        {
            var item = db.Opleidingen.Find(formulier.Id);
            if (item is null) return NotFound();
            item.Periode = formulier.Periode;
            item.Titel = formulier.Titel;
            item.Instelling = formulier.Instelling;
            item.Locatie = formulier.Locatie;
            item.Omschrijving = formulier.Omschrijving;
            item.Volgorde = formulier.Volgorde;
            item.IsGepubliceerd = formulier.IsGepubliceerd;
        }
        db.SaveChanges();
        TempData["Melding"] = "Opleiding opgeslagen.";
        return RedirectToAction(nameof(Opleidingen));
    }

    [HttpGet("opleidingen/{id:int}/bewerken")]
    public IActionResult OpleidingBewerken(int id)
    {
        var item = db.Opleidingen.Find(id);
        if (item is null) return NotFound();
        ViewData["Titel"] = "Opleiding beheren";
        ViewBag.Lijst = db.Opleidingen.OrderBy(o => o.Volgorde).ToList();
        return View("OpleidingSectie", item);
    }

    [HttpPost("opleidingen/{id:int}/verwijderen")]
    [ValidateAntiForgeryToken]
    public IActionResult OpleidingVerwijderen(int id)
    {
        var item = db.Opleidingen.Find(id);
        if (item is not null)
        {
            db.Opleidingen.Remove(item);
            db.SaveChanges();
            TempData["Melding"] = "Opleiding verwijderd.";
        }
        return RedirectToAction(nameof(Opleidingen));
    }

    /* ---------- Stage ---------- */

    [HttpGet("stages")]
    public IActionResult Stages()
    {
        ViewData["Titel"] = "Stage beheren";
        ViewBag.Lijst = db.Stages.OrderBy(s => s.Volgorde).Include(s => s.Rapporten).ToList();
        return View("StageSectie", new Stage());
    }

    [HttpPost("stages/opslaan")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(80 * 1024 * 1024)]
    public async Task<IActionResult> StageOpslaan(Stage formulier, DateOnly? startDatum)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Lijst = db.Stages.OrderBy(s => s.Volgorde).Include(s => s.Rapporten).ToList();
            return View("StageSectie", formulier);
        }

        if (formulier.Id == 0)
        {
            formulier.Volgorde = db.Stages.Count() + 1;
            formulier.StartDatum = startDatum is null ? null : startDatum.Value.ToDateTime(TimeOnly.MinValue);
            db.Stages.Add(formulier);
        }
        else
        {
            var item = db.Stages.Find(formulier.Id);
            if (item is null) return NotFound();
            item.Periode = formulier.Periode;
            item.Bedrijf = formulier.Bedrijf;
            item.Functie = formulier.Functie;
            item.Locatie = formulier.Locatie;
            item.Omschrijving = formulier.Omschrijving;
            item.Taken = formulier.Taken;
            item.Volgorde = formulier.Volgorde;
            item.IsGepubliceerd = formulier.IsGepubliceerd;
            item.Schooljaar = formulier.Schooljaar;
            item.StartDatum = startDatum is null ? null : startDatum.Value.ToDateTime(TimeOnly.MinValue);
        }

        db.SaveChanges();
        TempData["Melding"] = "Stage opgeslagen.";
        return RedirectToAction(nameof(Stages));
    }

    [HttpGet("stages/{id:int}/bewerken")]
    public IActionResult StageBewerken(int id)
    {
        var item = db.Stages.Include(s => s.Rapporten).FirstOrDefault(s => s.Id == id);
        if (item is null) return NotFound();
        ViewData["Titel"] = "Stage beheren";
        ViewBag.Lijst = db.Stages.OrderBy(s => s.Volgorde).Include(s => s.Rapporten).ToList();
        return View("StageSectie", item);
    }

    [HttpPost("stages/{id:int}/verwijderen")]
    [ValidateAntiForgeryToken]
    public IActionResult StageVerwijderen(int id)
    {
        var item = db.Stages.Include(s => s.Rapporten).FirstOrDefault(s => s.Id == id);
        if (item is not null)
        {
            foreach (var rapport in item.Rapporten)
            {
                uploads.Verwijder(rapport.Pad);
            }
            db.Stages.Remove(item);
            db.SaveChanges();
            TempData["Melding"] = "Stage verwijderd.";
        }
        return RedirectToAction(nameof(Stages));
    }

    /* ---------- Dagrapporten (Stage én POV) ---------- */

    [HttpPost("stages/{id:int}/rapporten")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(80 * 1024 * 1024)]
    public async Task<IActionResult> StageRapportToevoegen(int id, List<IFormFile>? rapportBestanden, string? rapportTitels, string? rapportDatums)
    {
        var stage = db.Stages.Find(id);
        if (stage is null) return NotFound();

        var fout = await VoegRapportenToeAsync(db, uploads, "stage", id,
            rapportBestanden, rapportTitels, rapportDatums,
            n => db.Rapporten.Where(r => r.StageId == id).Max(r => (int?)r.Volgorde) ?? 0);
        if (fout is not null)
        {
            TempData["Foutmelding"] = fout;
            return RedirectToAction(nameof(StageBewerken), new { id });
        }

        TempData["Melding"] = "Dagrapport(en) toegevoegd.";
        return RedirectToAction(nameof(StageBewerken), new { id });
    }

    [HttpPost("rapporten/{id:int}/verwijderen")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RapportVerwijderen(int id, string? terugUrl)
    {
        var rapport = db.Rapporten.Find(id);
        if (rapport is not null)
        {
            uploads.Verwijder(rapport.Pad);
            db.Rapporten.Remove(rapport);
            db.SaveChanges();
            TempData["Melding"] = "Dagrapport verwijderd.";
        }
        var veiligeTerug = Url.IsLocalUrl(terugUrl) ? terugUrl : Url.Action(nameof(Stages))!;
        return Redirect(veiligeTerug);
    }

    [HttpPost("rapporten/{id:int}/vervangen")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(80 * 1024 * 1024)]
    public async Task<IActionResult> RapportVervangen(int id, IFormFile? vervangBestand, string? terugUrl)
    {
        var rapport = db.Rapporten.Find(id);
        if (rapport is null) return NotFound();
        if (vervangBestand is null || vervangBestand.Length == 0)
        {
            TempData["Foutmelding"] = "Kies eerst een nieuw PDF-bestand.";
            return Redirect(Url.IsLocalUrl(terugUrl) ? terugUrl : Url.Action(nameof(Stages))!);
        }

        try
        {
            var nieuwPad = await uploads.SlaDocumentOpAsync(vervangBestand, "dagrapport");
            if (nieuwPad is not null)
            {
                uploads.Verwijder(rapport.Pad);
                rapport.Pad = nieuwPad;
                db.SaveChanges();
                TempData["Melding"] = "Dagrapport vervangen.";
            }
        }
        catch (InvalidOperationException fout)
        {
            TempData["Foutmelding"] = fout.Message;
        }
        return Redirect(Url.IsLocalUrl(terugUrl) ? terugUrl : Url.Action(nameof(Stages))!);
    }

    [HttpPost("rapporten/{id:int}/opslaan")]
    [ValidateAntiForgeryToken]
    public IActionResult RapportOpslaan(int id, string? rapportTitel, DateOnly? rapportDatum, bool isGepubliceerd, string? terugUrl)
    {
        var rapport = db.Rapporten.Find(id);
        if (rapport is not null)
        {
            rapport.Titel = string.IsNullOrWhiteSpace(rapportTitel) ? null : rapportTitel.Trim();
            if (rapportDatum is not null) rapport.Datum = rapportDatum.Value.ToDateTime(TimeOnly.MinValue);
            rapport.IsGepubliceerd = isGepubliceerd;
            db.SaveChanges();
            TempData["Melding"] = "Dagrapport bijgewerkt.";
        }
        return Redirect(Url.IsLocalUrl(terugUrl) ? terugUrl : Url.Action(nameof(Stages))!);
    }

    /* ---------- POV-locaties ---------- */

    [HttpGet("pov")]
    public IActionResult PovLocaties()
    {
        ViewData["Titel"] = "POV beheren";
        ViewBag.Lijst = db.PovLocaties.OrderBy(p => p.Volgorde).Include(p => p.Rapporten).ToList();
        return View("PovSectie", new PovLocatie());
    }

    [HttpPost("pov/opslaan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PovOpslaan(PovLocatie formulier)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(formulier.Naam))
        {
            if (!ModelState.IsValid) ModelState.AddModelError(string.Empty, "Naam is verplicht.");
            ViewBag.Lijst = db.PovLocaties.OrderBy(p => p.Volgorde).Include(p => p.Rapporten).ToList();
            return View("PovSectie", formulier);
        }

        if (formulier.Id == 0)
        {
            formulier.Slug = ZetSlug("", formulier.Naam);
            var basis = formulier.Slug;
            var n = 2;
            while (db.PovLocaties.Any(p => p.Slug == formulier.Slug))
            {
                formulier.Slug = $"{basis}-{n}";
                n++;
            }
            formulier.Volgorde = db.PovLocaties.Count() + 1;
            db.PovLocaties.Add(formulier);
            await db.SaveChangesAsync();
            return RedirectToAction(nameof(PovBewerken), new { id = formulier.Id });
        }

        var item = await db.PovLocaties.FindAsync(formulier.Id);
        if (item is null) return NotFound();
        item.Naam = formulier.Naam;
        item.Bedrijf = formulier.Bedrijf;
        item.Adres = formulier.Adres;
        item.Periode = formulier.Periode;
        item.Omschrijving = formulier.Omschrijving;
        item.Volgorde = formulier.Volgorde;
        item.IsGepubliceerd = formulier.IsGepubliceerd;
        item.Schooljaar = formulier.Schooljaar;
        await db.SaveChangesAsync();
        TempData["Melding"] = "POV-locatie opgeslagen.";
        return RedirectToAction(nameof(PovBewerken), new { id = item.Id });
    }

    [HttpGet("pov/{id:int}/bewerken")]
    public IActionResult PovBewerken(int id)
    {
        var item = db.PovLocaties.Include(p => p.Rapporten).FirstOrDefault(p => p.Id == id);
        if (item is null) return NotFound();
        ViewData["Titel"] = "POV-locatie beheren";
        ViewBag.Lijst = db.PovLocaties.OrderBy(p => p.Volgorde).Include(p => p.Rapporten).ToList();
        return View("PovSectie", item);
    }

    [HttpPost("pov/{id:int}/verwijderen")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PovVerwijderen(int id)
    {
        var item = db.PovLocaties.Include(p => p.Rapporten).FirstOrDefault(p => p.Id == id);
        if (item is not null)
        {
            foreach (var rapport in item.Rapporten)
            {
                uploads.Verwijder(rapport.Pad);
            }
            db.PovLocaties.Remove(item);
            await db.SaveChangesAsync();
            TempData["Melding"] = "POV-locatie verwijderd.";
        }
        return RedirectToAction(nameof(PovLocaties));
    }

    [HttpPost("pov/{id:int}/rapporten")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(80 * 1024 * 1024)]
    public async Task<IActionResult> PovRapportToevoegen(int id, List<IFormFile>? rapportBestanden, string? rapportTitels, string? rapportDatums)
    {
        var locatie = db.PovLocaties.Find(id);
        if (locatie is null) return NotFound();

        var fout = await VoegRapportenToeAsync(db, uploads, "pov", id,
            rapportBestanden, rapportTitels, rapportDatums,
            n => db.Rapporten.Where(r => r.PovLocatieId == id).Max(r => (int?)r.Volgorde) ?? 0);
        if (fout is not null)
        {
            TempData["Foutmelding"] = fout;
        }
        else
        {
            TempData["Melding"] = "Dagrapport(en) toegevoegd.";
        }
        return RedirectToAction(nameof(PovBewerken), new { id });
    }

    /* ---------- Werkervaring ---------- */

    [HttpGet("werkervaringen")]
    public IActionResult Werkervaringen()
    {
        ViewData["Titel"] = "Werkervaring beheren";
        ViewBag.Lijst = db.Werkervaringen.OrderBy(w => w.Volgorde).ToList();
        return View("WerkervaringSectie", new Werkervaring());
    }

    [HttpPost("werkervaringen/opslaan")]
    [ValidateAntiForgeryToken]
    public IActionResult WerkervaringOpslaan(Werkervaring formulier)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Lijst = db.Werkervaringen.OrderBy(w => w.Volgorde).ToList();
            return View("WerkervaringSectie", formulier);
        }

        if (formulier.Id == 0)
        {
            formulier.Volgorde = db.Werkervaringen.Count() + 1;
            db.Werkervaringen.Add(formulier);
        }
        else
        {
            var item = db.Werkervaringen.Find(formulier.Id);
            if (item is null) return NotFound();
            item.Periode = formulier.Periode;
            item.Bedrijf = formulier.Bedrijf;
            item.Functie = formulier.Functie;
            item.Locatie = formulier.Locatie;
            item.Omschrijving = formulier.Omschrijving;
            item.Volgorde = formulier.Volgorde;
            item.IsGepubliceerd = formulier.IsGepubliceerd;
        }
        db.SaveChanges();
        TempData["Melding"] = "Werkervaring opgeslagen.";
        return RedirectToAction(nameof(Werkervaringen));
    }

    [HttpGet("werkervaringen/{id:int}/bewerken")]
    public IActionResult WerkervaringBewerken(int id)
    {
        var item = db.Werkervaringen.Find(id);
        if (item is null) return NotFound();
        ViewData["Titel"] = "Werkervaring beheren";
        ViewBag.Lijst = db.Werkervaringen.OrderBy(w => w.Volgorde).ToList();
        return View("WerkervaringSectie", item);
    }

    [HttpPost("werkervaringen/{id:int}/verwijderen")]
    [ValidateAntiForgeryToken]
    public IActionResult WerkervaringVerwijderen(int id)
    {
        var item = db.Werkervaringen.Find(id);
        if (item is not null)
        {
            db.Werkervaringen.Remove(item);
            db.SaveChanges();
            TempData["Melding"] = "Werkervaring verwijderd.";
        }
        return RedirectToAction(nameof(Werkervaringen));
    }

    /* ---------- Vrije inleidingsteksten ---------- */

    [HttpGet("inhoud")]
    public IActionResult Inhoud()
    {
        ViewData["Titel"] = "Inleidingsteksten";
        ViewBag.Sleutels = Sleutels();
        return View(db.PaginaInhouden.ToList());
    }

    [HttpPost("inhoud/opslaan")]
    [ValidateAntiForgeryToken]
    public IActionResult InhoudOpslaan(string sleutel, string inhoud)
    {
        if (string.IsNullOrWhiteSpace(sleutel))
        {
            return RedirectToAction(nameof(Inhoud));
        }

        var bestaande = db.PaginaInhouden.Find(sleutel);
        if (bestaande is null)
        {
            db.PaginaInhouden.Add(new PaginaInhoud { Sleutel = Sleutel(sleutel), Inhoud = inhoud ?? "" });
        }
        else
        {
            bestaande.Inhoud = inhoud ?? "";
        }
        db.SaveChanges();
        TempData["Melding"] = $"Tekst voor “{sleutel}” opgeslagen als “{Sleutel(sleutel)}”.";
        return RedirectToAction(nameof(Inhoud));
    }

    /* ---------- FAQ ---------- */

    [HttpGet("faq")]
    public IActionResult Faq()
    {
        ViewData["Titel"] = "FAQ beheren";
        ViewBag.Lijst = db.FaqItems.OrderBy(f => f.Volgorde).ThenBy(f => f.Id).ToList();
        return View("Faq", new FaqItem());
    }

    [HttpGet("faq/{id:int}/bewerken")]
    public IActionResult FaqBewerken(int id)
    {
        var item = db.FaqItems.Find(id);
        if (item is null) return NotFound();
        ViewData["Titel"] = "FAQ beheren";
        ViewBag.Lijst = db.FaqItems.OrderBy(f => f.Volgorde).ThenBy(f => f.Id).ToList();
        return View("Faq", item);
    }

    [HttpPost("faq/opslaan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FaqOpslaan(FaqItem formulier)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Lijst = db.FaqItems.OrderBy(f => f.Volgorde).ThenBy(f => f.Id).ToList();
            return View("Faq", formulier);
        }

        formulier.Vraag = formulier.Vraag.Trim();
        formulier.Antwoord = formulier.Antwoord.Trim();

        if (formulier.Id == 0)
        {
            formulier.Volgorde = db.FaqItems.Count() + 1;
            db.FaqItems.Add(formulier);
        }
        else
        {
            var item = await db.FaqItems.FindAsync(formulier.Id);
            if (item is null) return NotFound();
            item.Vraag = formulier.Vraag;
            item.Antwoord = formulier.Antwoord;
            item.IsGepubliceerd = formulier.IsGepubliceerd;
            item.Volgorde = formulier.Volgorde;
        }

        db.SaveChanges();
        TempData["Melding"] = "FAQ-item opgeslagen.";
        return RedirectToAction(nameof(Faq));
    }

    [HttpPost("faq/{id:int}/verwijderen")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FaqVerwijderen(int id)
    {
        var item = await db.FaqItems.FindAsync(id);
        if (item is not null)
        {
            db.FaqItems.Remove(item);
            db.SaveChanges();
            TempData["Melding"] = "FAQ-item verwijderd.";
        }
        return RedirectToAction(nameof(Faq));
    }

    /* ---------- Downloads ---------- */

    [HttpGet("downloads")]
    public IActionResult Downloads()
    {
        ViewData["Titel"] = "Downloads beheren";
        ViewBag.Lijst = db.DownloadItems.OrderBy(d => d.Volgorde).ThenBy(d => d.Id).ToList();
        return View("Downloads", new DownloadItem());
    }

    [HttpGet("downloads/{id:int}/bewerken")]
    public IActionResult DownloadsBewerken(int id)
    {
        var item = db.DownloadItems.Find(id);
        if (item is null) return NotFound();
        ViewData["Titel"] = "Downloads beheren";
        ViewBag.Lijst = db.DownloadItems.OrderBy(d => d.Volgorde).ThenBy(d => d.Id).ToList();
        return View("Downloads", item);
    }

    [HttpPost("downloads/opslaan")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(24 * 1024 * 1024)]
    public async Task<IActionResult> DownloadsOpslaan(DownloadItem formulier, IFormFile? bestand)
    {
        // Op “bewerken” is een bestaand bestand voldoende; een nieuwe rij MOET een bestand hebben.
        var nieuwPad = await uploads.SlaDownloadOpAsync(bestand);
        var bestandLeeg = formulier.Id == 0 && string.IsNullOrWhiteSpace(nieuwPad);
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(formulier.Titel) || bestandLeeg)
        {
            if (string.IsNullOrWhiteSpace(formulier.Titel)) ModelState.AddModelError("Titel", "Kies een titel.");
            if (bestandLeeg) ModelState.AddModelError("BestandPad", "Kies een bestand.");
            ViewBag.Lijst = db.DownloadItems.OrderBy(d => d.Volgorde).ThenBy(d => d.Id).ToList();
            return View("Downloads", formulier);
        }

        formulier.Titel = formulier.Titel.Trim();

        if (formulier.Id == 0)
        {
            formulier.BestandPad = nieuwPad!;
            formulier.Volgorde = db.DownloadItems.Count() + 1;
            db.DownloadItems.Add(formulier);
        }
        else
        {
            var item = await db.DownloadItems.FindAsync(formulier.Id);
            if (item is null) return NotFound();
            if (nieuwPad is not null)
            {
                uploads.Verwijder(item.BestandPad);
                item.BestandPad = nieuwPad;
            }
            item.Titel = formulier.Titel;
            item.Omschrijving = formulier.Omschrijving;
            item.IsGepubliceerd = formulier.IsGepubliceerd;
            item.Volgorde = formulier.Volgorde;
        }

        db.SaveChanges();
        TempData["Melding"] = "Download opgeslagen.";
        return RedirectToAction(nameof(Downloads));
    }

    [HttpPost("downloads/{id:int}/verwijderen")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadVerwijderen(int id)
    {
        var item = await db.DownloadItems.FindAsync(id);
        if (item is not null)
        {
            uploads.Verwijder(item.BestandPad);
            db.DownloadItems.Remove(item);
            db.SaveChanges();
            TempData["Melding"] = "Download verwijderd.";
        }
        return RedirectToAction(nameof(Downloads));
    }

    /* ---------- AREI ---------- */

    [HttpGet("arei")]
    public IActionResult Arei()
    {
        ViewData["Titel"] = "AREI beheren";
        ViewBag.Lijst = db.AreiBoeken.OrderBy(b => b.Volgorde).ToList();
        return View("Arei");
    }

    [HttpPost("arei/{id:int}/opslaan")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(24 * 1024 * 1024)]
    public async Task<IActionResult> AreiOpslaan(int id, string beschrijving, IFormFile? bestand)
    {
        var boek = await db.AreiBoeken.FindAsync(id);
        if (boek is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(beschrijving))
        {
            boek.Beschrijving = beschrijving.Trim();
        }

        try
        {
            var nieuwPad = await uploads.SlaDocumentOpAsync(bestand, "arei");
            if (nieuwPad is not null)
            {
                uploads.Verwijder(boek.BestandPad);
                boek.BestandPad = nieuwPad;
            }
        }
        catch (InvalidOperationException fout)
        {
            TempData["Foutmelding"] = fout.Message;
            return RedirectToAction(nameof(Arei));
        }

        db.SaveChanges();
        TempData["Melding"] = $"AREI-boek “{boek.Naam}” bijgewerkt.";
        return RedirectToAction(nameof(Arei));
    }

    /* ---------- Hulprichtlijnen ---------- */

    private SelectList Sleutels() => new(
        new Dictionary<string, string>
        {
            ["intro-projecten"] = "Inleiding bij Projecten",
            ["intro-schoolcarriere"] = "Inleiding bij Schoolcarrière",
            ["intro-stage"] = "Inleiding bij Stage",
            ["intro-werkervaring"] = "Inleiding bij Werkervaring",
            ["intro-overmij"] = "Inleiding bij Over mij",
            ["intro-contact"] = "Inleiding bij Contact"
        }, "Key", "Value");

    private static string Sleutel(string ruw) =>
        ruw.Trim().ToLowerInvariant().Replace(" ", "");

    private static string ZetSlug(string aangeleverde, string titel)
    {
        var bron = string.IsNullOrWhiteSpace(aangeleverde) ? titel : aangeleverde;
        var normaal = bron.Normalize(NormalizationForm.FormD);
        var tekenreeks = new StringBuilder();
        foreach (var teken in normaal)
        {
            var categorie = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(teken);
            if (categorie == System.Globalization.UnicodeCategory.NonSpacingMark) continue;

            var kleineLetter = char.ToLowerInvariant(teken);
            if ((kleineLetter >= 'a' && kleineLetter <= 'z') || char.IsDigit(kleineLetter))
            {
                tekenreeks.Append(kleineLetter);
            }
            else if (kleineLetter is ' ' or '-' or '_')
            {
                tekenreeks.Append('-');
            }
        }
        return tekenreeks.ToString().Trim('-');
    }

    /// <summary>
    /// Gemeenschappelijke rapport-upload voor Stage- én POV-rapporten.
    /// Elke bestand krijgt een datum en optionele titel; volgorde wordt uit de bestaande lijst afgeleid.
    /// </summary>
    private static async Task<string?> VoegRapportenToeAsync(
        SiteDbContext db, UploadService uploads, string onderwerp, int ownerId,
        List<IFormFile>? bestanden, string? titels, string? datums, Func<int, int> volgendeVolgorde)
    {
        if (bestanden is null || bestanden.Count == 0) return null;

        var titellijst = (titels ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var datumlijst = (datums ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var volgorde = volgendeVolgorde(ownerId);

        for (var i = 0; i < bestanden.Count; i++)
        {
            var bestand = bestanden[i];
            try
            {
                var pad = await uploads.SlaDocumentOpAsync(bestand, $"{onderwerp}-rapport");
                if (pad is null) continue;

                var datum = DateTime.Today;
                if (i < datumlijst.Length && DateOnly.TryParse(datumlijst[i], out var geparseerd))
                {
                    datum = geparseerd.ToDateTime(TimeOnly.MinValue);
                }
                else if (DateOnly.TryParse(datumlijst.Length >= 1 ? datumlijst[^1] : null, out var gezamenlijk) && datumlijst.Length == 1)
                {
                    datum = gezamenlijk.ToDateTime(TimeOnly.MinValue);
                }

                db.Rapporten.Add(new Rapport
                {
                    StageId = onderwerp == "stage" ? ownerId : null,
                    PovLocatieId = onderwerp == "pov" ? ownerId : null,
                    Pad = pad,
                    Titel = i < titellijst.Length && !string.IsNullOrWhiteSpace(titellijst[i]) ? titellijst[i] : null,
                    Datum = datum,
                    IsGepubliceerd = true,
                    Volgorde = ++volgorde
                });
            }
            catch (InvalidOperationException fout)
            {
                return $"{bestand.FileName}: {fout.Message}";
            }
        }
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>
    /// Slaat een upload op in de juiste soort (afbeelding, video of pdf/tekening)
    /// en geeft (pad, soort) terug.
    /// </summary>
    private async Task<(string? Pad, string Soort)> SlaMediaOpAsync(IFormFile bestand, string onderwerp)
    {
        var extensie = Path.GetExtension(bestand.FileName).ToLowerInvariant();
        if (extensie is ".mp4" or ".webm" or ".mov")
        {
            return (await uploads.SlaVideoOpAsync(bestand, onderwerp), "video");
        }
        if (extensie is ".pdf")
        {
            return (await uploads.SlaDocumentOpAsync(bestand, onderwerp), "document");
        }
        return (await uploads.SlaOpAsync(bestand, onderwerp), "afbeelding");
    }

    private async Task VulGallerijUitUploadsAsync(Project project, List<IFormFile>? bestanden, string? bijschriftenTekst)
    {
        var bijschriften = SplitsBijschriften(bijschriftenTekst);
        var volgorde = 1;
        if (bestanden is null) return;
        foreach (var bestand in bestanden)
        {
            var (pad, soort) = await SlaMediaOpAsync(bestand, "gallerij");
            if (pad is null) continue;
            project.Afbeeldingen.Add(new GallerijAfbeelding
            {
                ProjectId = project.Id,
                Pad = pad,
                Soort = soort,
                Onderschrift = volgorde <= bijschriften.Length && !string.IsNullOrWhiteSpace(bijschriften[volgorde - 1])
                    ? bijschriften[volgorde - 1].Trim()
                    : null,
                Volgorde = volgorde
            });
            volgorde++;
        }
    }

    private async Task VoegGallerijUploadsToeAsync(Project project, List<IFormFile>? bestanden, string? bijschriftenTekst, int startVolgorde)
    {
        var bijschriften = SplitsBijschriften(bijschriftenTekst);
        var volgorde = startVolgorde + 1;
        if (bestanden is null) return;
        foreach (var bestand in bestanden)
        {
            var (pad, soort) = await SlaMediaOpAsync(bestand, "gallerij");
            if (pad is null) continue;
            var bijschriftIndex = volgorde - startVolgorde - 1;
            var nieuwe = new GallerijAfbeelding
            {
                ProjectId = project.Id,
                Pad = pad,
                Soort = soort,
                Onderschrift = bijschriftIndex < bijschriften.Length && !string.IsNullOrWhiteSpace(bijschriften[bijschriftIndex])
                    ? bijschriften[bijschriftIndex].Trim()
                    : null,
                Volgorde = volgorde
            };
            project.Afbeeldingen.Add(nieuwe);
            volgorde++;
        }
    }

    private static string[] SplitsBijschriften(string? tekst) =>
        (tekst ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public class EmailWeergave
    {
        public string Adres { get; set; } = "";
        public string Voorstelling { get; set; } = "";

        public string SmtpServer { get; set; } = "";
        public string SmtpPoort { get; set; } = "";
        public string SmtpGebruiker { get; set; } = "";
        public bool SmtpGesteld { get; set; }

        public string GmailAdres { get; set; } = "";
        public bool GmailIsGekoppeld { get; set; }
    }

    public class DashboardWeergave
    {
        public int AantalProjecten { get; set; }
        public int AantalOngelezenBerichten { get; set; }
        public int AantalOpleidingen { get; set; }
        public int AantalWerkervaringen { get; set; }
        public int AantalPovLocaties { get; set; }
        public List<ContactBericht> LaatsteBerichten { get; set; } = new();
    }
}
