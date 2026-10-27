using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortfolio.Data;
using SchoolPortfolio.Models;

namespace SchoolPortfolio.Controllers;

public class ProjectenController(SiteDbContext db) : Controller
{
    private static readonly string[] BekendeJaren = { "3de jaar", "4de jaar", "5de jaar", "6de jaar" };

    private static string? JaarVanSlug(string? slug)
    {
        var jaar = (slug ?? "").Replace('-', ' ').Trim();
        return BekendeJaren.Contains(jaar, StringComparer.OrdinalIgnoreCase)
            ? BekendeJaren.First(j => j.Equals(jaar, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private static string VakVanSlug(string? slug) => (slug ?? "").Replace('-', ' ').ToLowerInvariant() switch
    {
        "elektriciteit" => "Elektriciteit",
        "constructie" => "Constructie",
        "constructietechnieken" => "Constructietechnieken",
        _ => ""
    };

    // ---------- Alle projecten ----------
    [Route("projecten")]
    public IActionResult Index()
    {
        ViewData["Titel"] = "Projecten";
        var projecten = db.Projecten
            .Include(p => p.Afbeeldingen)
            .OrderBy(p => p.Schooljaar)
            .ThenBy(p => p.Vak)
            .ThenBy(p => p.Volgorde)
            .ThenBy(p => p.Titel)
            .ToList();
        return View("Index", projecten);
    }

    // ---------- Projecten van één schooljaar ----------
    // Regex-constraint zodat /projecten/{slug} (projectdetail) niet met dit jaarroute botst.
    [Route("projecten/{jaarSlug:regex(^(3de|4de|5de|6de)-jaar$)}")]
    public IActionResult PerJaar(string jaarSlug)
    {
        var jaar = JaarVanSlug(jaarSlug);
        if (jaar is null) return NotFound();
        return FilterLijst(jaar, null, jaar);
    }

    // ---------- Projecten van één vak binnen een schooljaar ----------
    [Route("projecten/{jaarSlug:regex(^(3de|4de|5de|6de)-jaar$)}/{vakSlug:regex(^(elektriciteit|constructie|constructietechnieken)$)}")]
    public IActionResult PerJaarEnVak(string jaarSlug, string vakSlug)
    {
        var jaar = JaarVanSlug(jaarSlug);
        var vak = VakVanSlug(vakSlug);
        if (jaar is null || vak == "") return NotFound();
        return FilterLijst(jaar, vak, $"{jaar} · {vak}");
    }

    private IActionResult FilterLijst(string jaar, string? vak, string titel)
    {
        ViewData["Titel"] = titel;
        ViewBag.FilterJaar = jaar;
        ViewBag.FilterVak = vak;
        var query = db.Projecten
            .Include(p => p.Afbeeldingen)
            .Where(p => p.Schooljaar == jaar);
        if (vak is not null) query = query.Where(p => p.Vak == vak);
        var projecten = query
            .OrderBy(p => p.Volgorde)
            .ThenBy(p => p.Titel)
            .ToList();
        if (projecten.Count == 0) return NotFound();
        return View("Index", projecten);
    }

    // ---------- Projectdetail ----------
    // Lookahead zorgt dat jaarslugs hier niet méé matchen (geen ambiguïteit).
    [Route("projecten/{slug:regex(^(?!3de-jaar$|4de-jaar$|5de-jaar$|6de-jaar$))}")]
    public IActionResult Detail(string slug)
    {
        var project = db.Projecten
            .Include(p => p.Afbeeldingen)
            .FirstOrDefault(p => p.Slug == slug);

        if (project is null)
        {
            return NotFound();
        }

        ViewData["Titel"] = project.Titel;
        return View(project);
    }
}
