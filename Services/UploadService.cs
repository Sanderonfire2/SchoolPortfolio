using Microsoft.AspNetCore.Http;
using PDFtoImage;
using SkiaSharp;

namespace SchoolPortfolio.Services;

public class UploadService(IWebHostEnvironment omgeving, ILogger<UploadService> logger)
{
    // .svg is bewust uitgesloten: een QA-upload kan scriptcode bevatten (opgeslagen XSS).
    // Eerste-SVG's die de site zelf gebruikt, staan statisch in wwwroot/img.
    private static readonly string[] ToegestaneBeelden =
        [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    private static readonly string[] ToegestaneVideos =
        [".mp4", ".webm", ".mov"];

    private static readonly string[] ToegestaneDocumenten = [".pdf"];

    private static readonly string[] ToegestaneDownloads =
        [".pdf", ".zip", ".docx", ".xlsx", ".txt", ".png", ".jpg", ".jpeg", ".webp"];

    private const long MaximaleGrootte = 8 * 1024 * 1024;   // 8 MB per afbeelding
    private const long MaximaleVideoGrootte = 64 * 1024 * 1024; // 64 MB per video
    private const long MaximaleDocumentGrootte = 16 * 1024 * 1024; // 16 MB per PDF
    private const long MaximaleDownloadGrootte = 24 * 1024 * 1024; // 24 MB per download

    /// <summary>
    /// Slaat een afbeelding op en geeft het publieke pad terug, bijvoorbeeld "/uploads/beeld-gallerij-3f2a.png".
    /// </summary>
    public async Task<string?> SlaOpAsync(IFormFile? bestand, string onderwerp)
    {
        if (bestand is null || bestand.Length == 0) return null;

        return await SlaOpInternAsync(bestand, ToegestaneBeelden, "beeld", onderwerp);
    }

    /// <summary>
    /// Slaat een video op en geeft het publieke pad terug.
    /// </summary>
    public async Task<string?> SlaVideoOpAsync(IFormFile? bestand, string onderwerp)
    {
        if (bestand is null || bestand.Length == 0) return null;

        return await SlaOpInternAsync(bestand, ToegestaneVideos, "video", onderwerp);
    }

    /// <summary>
    /// Slaat een document (pdf/tekening) op en geeft het publieke pad terug.
    /// </summary>
    public async Task<string?> SlaDocumentOpAsync(IFormFile? bestand, string onderwerp)
    {
        if (bestand is null || bestand.Length == 0) return null;

        return await SlaOpInternAsync(bestand, ToegestaneDocumenten, "document", onderwerp);
    }

    /// <summary>
    /// Slaat een willekeurig downloadbestand op (pdf, zip, office, afbeelding)
    /// en geeft het publieke pad terug.
    /// </summary>
    public async Task<string?> SlaDownloadOpAsync(IFormFile? bestand)
    {
        if (bestand is null || bestand.Length == 0) return null;

        return await SlaOpInternAsync(bestand, ToegestaneDownloads, "download", "downloads");
    }

    private async Task<string> SlaOpInternAsync(
        IFormFile bestand, string[] toegestaneExtensies, string soort, string onderwerp)
    {
        var extensie = Path.GetExtension(bestand.FileName).ToLowerInvariant();
        if (!toegestaneExtensies.Contains(extensie))
        {
            logger.LogWarning("Upload geweigerd: {Type} bestand {File} met extensie {Ext}", soort,
                bestand.FileName?.Length > 60 ? bestand.FileName[..60] + "…" : bestand.FileName,
                extensie);
            throw new InvalidOperationException(
                $"Ongeldig {onderwerp}-bestandstype: “{extensie}”. Toegestaan: {string.Join(", ", toegestaneExtensies)}.");
        }

        var maximum = soort == "video" ? MaximaleVideoGrootte
            : soort == "download" ? MaximaleDownloadGrootte
            : soort == "document" ? MaximaleDocumentGrootte
            : MaximaleGrootte;
        if (bestand.Length > maximum)
        {
            throw new InvalidOperationException(
                $"“{bestand.FileName}” is te groot ({GrootteAlsTekst(bestand.Length)}). Maximum: {GrootteAlsTekst(maximum)}.");
        }

        var uploadsMap = Path.Combine(omgeving.WebRootPath ?? "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsMap);

        var nieuweNaam = $"{soort}-{onderwerp.ToLowerInvariant()}-{Guid.NewGuid().ToString("N")[..8]}{extensie}";
        var doelpad = Path.Combine(uploadsMap, nieuweNaam);

        await using var stroom = new FileStream(doelpad, FileMode.Create);
        await bestand.CopyToAsync(stroom);

        logger.LogInformation("Bestand opgeslagen: {Bestand} ({Grootte})", nieuweNaam, GrootteAlsTekst(bestand.Length));

        if (soort == "document")
        {
            MaakPdfVoorvertoning(doelpad, nieuweNaam);
        }

        return $"/uploads/{nieuweNaam}";
    }

    /// <summary>
    /// Genereert een kleine PNG-voorvertoning van de eerste pagina van een PDF
    /// in /uploads/voorvertoningen. Mislukt dit, dan blijft de upload zelf gewoon bestaan;
    /// de weergave valt netjes terug op de documentnaam.
    /// </summary>
    private void MaakPdfVoorvertoning(string pdfPad, string pdfNaam)
    {
        try
        {
            var map = Path.Combine(omgeving.WebRootPath ?? "wwwroot", "uploads", "voorvertoningen");
            Directory.CreateDirectory(map);
            var voorName = Path.GetFileNameWithoutExtension(pdfNaam) + ".png";
            var voorPad = Path.Combine(map, voorName);

            using var stroom = File.OpenRead(pdfPad);
            var configuratie = new RenderOptions(WithAnnotations: true);
            using var bitmap = Conversion.ToImage(stroom, page: 0, options: configuratie);
            var schaal = 480d / bitmap.Width;
            if (schaal >= 1) schaal = 1;
            using var verkleind = new SKBitmap(480, Math.Max(1, (int)Math.Round(bitmap.Height * schaal)));
            using var doek = new SKCanvas(verkleind);
            doek.DrawBitmap(bitmap, 0, 0, new SKPaint { FilterQuality = SKFilterQuality.Medium });
            doek.Flush();
            using var beeld = SKImage.FromBitmap(verkleind);
            using var gegevens = beeld.Encode(SKEncodedImageFormat.Png, 80);
            using var uit = File.Create(voorPad);
            gegevens.SaveTo(uit);
            logger.LogInformation("PDF-voorvertoning gemaakt: {Voor}", voorName);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Kon geen PDF-voorvertoning maken voor {Pdf}: {Fout} — de weergave valt terug op de documentnaam.",
                pdfNaam, ex.Message);
        }
    }

    /// <summary>
    /// Geeft het pad van de voorvertoning van een eerste terug, of null als die niet (meer) bestaat.
    /// </summary>
    public string? VoorvertoningVan(string? publiekPad)
    {
        if (string.IsNullOrWhiteSpace(publiekPad) || !publiekPad.StartsWith("/uploads/")) return null;
        if (!publiekPad.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return null;

        var naam = Path.GetFileNameWithoutExtension(publiekPad) + ".png";
        var volledigPad = Path.Combine(omgeving.WebRootPath ?? "wwwroot", "uploads", "voorvertoningen", naam);
        return File.Exists(volledigPad) ? $"/uploads/voorvertoningen/{naam}" : null;
    }

    /// <summary>
    /// Verwijdert een eerder opgeslagen bestand binnen /uploads (voorkomt padtraversal).
    /// </summary>
    public void Verwijder(string? publiekPad)
    {
        if (string.IsNullOrWhiteSpace(publiekPad) || !publiekPad.StartsWith("/uploads/")) return;

        var naam = Path.GetFileName(publiekPad);
        var volledigPad = Path.Combine(omgeving.WebRootPath ?? "wwwroot", "uploads", naam);
        try
        {
            if (File.Exists(volledigPad)) File.Delete(volledigPad);
            var voorNaam = Path.ChangeExtension(naam, ".png");
            var voorPath = Path.Combine(omgeving.WebRootPath ?? "wwwroot", "uploads", "voorvertoningen", voorNaam);
            if (File.Exists(voorPath)) File.Delete(voorPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Kon bestand {Bestand} niet verwijderen: {Fout}", naam, ex.Message);
        }
    }

    private static string GrootteAlsTekst(long bytes) =>
        bytes >= 1024 * 1024
            ? $"{bytes / 1024d / 1024d:F1} MB"
            : $"{bytes / 1024d:F0} KB";
}
