using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SchoolPortfolio.Data;
using SchoolPortfolio.Models;

namespace SchoolPortfolio.Controllers;

public class HomeController(SiteDbContext db) : Controller
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

    [Route("downloads")]
    public IActionResult Downloads() => Uitzicht("Downloads", "Downloads", null);

    [Route("contact")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("contactformulier")]
    public IActionResult Contact(ContactFormulier formulier)
    {
        if (!ModelState.IsValid)
        {
            return Uitzicht("Contact", "Contact", formulier);
        }

        db.ContactBerichten.Add(new ContactBericht
        {
            Naam = formulier.Naam.Trim(),
            Email = formulier.Email.Trim(),
            Onderwerp = formulier.Onderwerp.Trim(),
            Inhoud = formulier.Inhoud.Trim(),
            VerstuurdOp = DateTime.Now
        });
        db.SaveChanges();

        TempData["BevestigingBericht"] = "Bedankt voor je bericht! Ik reageer zo snel mogelijk.";
        return RedirectToAction(nameof(Contact));
    }

    [Route("fout")]
    public IActionResult Fout() => Uitzicht("Fout", "Fout", null);

    private IActionResult Uitzicht(string weergave, string titel, object? model)
    {
        ViewData["Titel"] = titel;
        return View(weergave, model);
    }
}
