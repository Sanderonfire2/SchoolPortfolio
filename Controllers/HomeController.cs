using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SchoolPortfolio.Data;
using SchoolPortfolio.Models;
using SchoolPortfolio.Services;

namespace SchoolPortfolio.Controllers;

public class HomeController(SiteDbContext db, EmailService email, IWebHostEnvironment omgeving) : Controller
{
    [Route("")]
    public IActionResult Index() => Uitzicht("Index", "Home", null);

    [Route("schoolcarriere")]
    public IActionResult Schoolcarriere() => Uitzicht("Schoolcarriere", "Schoolcarrière", null);

    [Route("stage")]
    public IActionResult Stage() => Uitzicht("Stage", "Stage", null);

    [Route("stage/{jaarSlug:regex(^(5de|6de)-jaar$)}")]
    public IActionResult StagePerJaar(string jaarSlug)
    {
        var jaar = jaarSlug.Replace('-', ' ');
        ViewData["Titel"] = $"Stage · {jaar}";
        ViewBag.FilterJaar = jaar;
        return View("Stage");
    }

    [Route("pov")]
    public IActionResult Pov() => Uitzicht("Pov", "POV", null);

    [Route("pov/{jaarSlug:regex(^(5de|6de)-jaar$)}")]
    public IActionResult PovPerJaar(string jaarSlug)
    {
        var jaar = jaarSlug.Replace('-', ' ');
        ViewData["Titel"] = $"POV · {jaar}";
        ViewBag.FilterJaar = jaar;
        return View("Pov");
    }

    [Route("werkervaring")]
    public IActionResult Werkervaring() => Uitzicht("Werkervaring", "Werkervaring", null);

    [Route("over-mij")]
    public IActionResult OverMij() => Uitzicht("OverMij", "Over mij", null);

    [Route("contact")]
    [HttpGet]
    public IActionResult Contact() => Uitzicht("Contact", "Contact", new ContactFormulier());

    [Route("faq")]
    public IActionResult Faq() => Uitzicht("Faq", "FAQ", null);

    [Route("arei")]
    public IActionResult Arei() => Uitzicht("Arei", "AREI", null);

    /// <summary>
    /// Levert een AREI-pdf rechtstreeks vanuit de applicatie in plaats van via een
    /// kaal bestandspad. Zo werkt "in de browser inlezen" ook wanneer het opgeslagen
    /// pad in een oude database naar een andere host of een verdwenen upload wijst.
    /// </summary>
    [Route("arei/{slug}/pdf")]
    public IActionResult AreiPdf(string slug, bool download = false)
    {
        var boek = db.AreiBoeken.FirstOrDefault(b => b.Slug == slug);
        if (boek is null)
        {
            return NotFound();
        }

        var fysiekPad = FysiekPadVan(boek.BestandPad) ?? StandaardAreiPad(boek.Slug);
        if (fysiekPad is null)
        {
            return NotFound();
        }

        var bestandsnaam = Path.GetFileName(fysiekPad);

        // enableRangeProcessing is nodig voor de ingebouwde pdf-lezer van de
        // browser: die vraagt het bestand in stukken op.
        return download
            ? PhysicalFile(fysiekPad, "application/pdf", bestandsnaam, enableRangeProcessing: true)
            : PhysicalFile(fysiekPad, "application/pdf", enableRangeProcessing: true);
    }

    /// <summary>
    /// Zet een opgeslagen publiek pad om naar een bestaand bestand onder wwwroot.
    /// Absolute url's (bijvoorbeeld een oude http://localhost:... uit een eerdere
    /// versie) worden teruggebracht tot hun pad; padtraversal wordt geweigerd.
    /// </summary>
    private string? FysiekPadVan(string? opgeslagenPad)
    {
        if (string.IsNullOrWhiteSpace(opgeslagenPad))
        {
            return null;
        }

        var pad = opgeslagenPad.Trim().Replace('\\', '/');

        if (Uri.TryCreate(pad, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            pad = uri.AbsolutePath;
        }

        if (pad.StartsWith("~/", StringComparison.Ordinal))
        {
            pad = pad[1..];
        }

        if (!pad.StartsWith('/'))
        {
            pad = "/" + pad;
        }

        if (pad.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        var webRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(omgeving.WebRootPath) ? "wwwroot" : omgeving.WebRootPath);
        var volledigPad = Path.GetFullPath(Path.Combine(webRoot, pad.TrimStart('/')));

        if (!volledigPad.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(volledigPad))
        {
            return null;
        }

        return volledigPad;
    }

    /// <summary>
    /// Terugvaloptie: het vaste pdf-bestand van het boek in wwwroot/downloaden/arei
    /// (boek-1-..., boek-2-..., boek-3-...).
    /// </summary>
    private string? StandaardAreiPad(string slug)
    {
        var nummer = System.Text.RegularExpressions.Regex.Match(slug, @"boek-(\d)");
        if (!nummer.Success)
        {
            return null;
        }

        var map = Path.Combine(omgeving.WebRootPath ?? "wwwroot", "downloaden", "arei");
        if (!Directory.Exists(map))
        {
            return null;
        }

        return Directory.EnumerateFiles(map, $"boek-{nummer.Groups[1].Value}-*.pdf")
            .OrderBy(p => p)
            .FirstOrDefault();
    }

    [Route("downloads")]
    public IActionResult Downloads() => Uitzicht("Downloads", "Downloads", null);

    [Route("contact")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("contactformulier")]
    public async Task<IActionResult> Contact(ContactFormulier formulier)
    {
        if (!ModelState.IsValid)
        {
            return Uitzicht("Contact", "Contact", formulier);
        }

        var bericht = new ContactBericht
        {
            Naam = formulier.Naam.Trim(),
            Email = formulier.Email.Trim(),
            Onderwerp = formulier.Onderwerp.Trim(),
            Inhoud = formulier.Inhoud.Trim(),
            VerstuurdOp = DateTime.Now
        };
        db.ContactBerichten.Add(bericht);
        db.SaveChanges();

        // Bevestiging aan de bezoeker + notificatie aan de site-eigenaar
        // (die gebruikmaken van het e-mailadres uit het Profiel-record).
        await email.VerstuurBevestigingAanBezoekerAsync(bericht);
        await email.VerstuurNotificatieAanBeheerderAsync(bericht);

        TempData["BevestigingBericht"] = "Bedankt voor je bericht! Ik reageer zo snel mogelijk.";
        return RedirectToAction(nameof(Contact));
    }

    [Route("fout")]
    public IActionResult Fout(int? code)
    {
        var status = code is >= 400 and <= 599 ? code.Value : 500;
        Response.StatusCode = status;
        ViewData["Foutcode"] = status;
        return Uitzicht("Fout", "Er ging iets mis", null);
    }

    private IActionResult Uitzicht(string weergave, string titel, object? model)
    {
        ViewData["Titel"] = titel;
        return View(weergave, model);
    }
}
