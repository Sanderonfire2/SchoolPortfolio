using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace SchoolPortfolio.Services;

/// <summary>
/// Beheert de gekoppelde Gmail-gegevens (Gmail-adres + Google App-wachtwoord).
///
/// De gegevens staan nooit in appsettings.json of in de repository. Ze worden
/// versleuteld met ASP.NET Core Data Protection bewaard in
/// <c>App_Data/gmail-geheimen.json</c>. Die map staat in .gitignore en blijft op
/// de server bewaard, zodat de koppeling een herstart overleeft.
///
/// Als er (nog) geen koppeling is, worden optioneel de configuratiesleutels
/// <c>Gmail:Adres</c> en <c>Gmail:AppWachtwoord</c> gebruikt
/// (omgevingsvariabelen <c>Gmail__Adres</c> en <c>Gmail__AppWachtwoord</c>).
/// </summary>
public sealed class GmailGeheimen
{
    private const string Bestandsnaam = "gmail-geheimen.json";
    private const string BeschermerDoel = "GmailGeheimen";

    private sealed class OpslagModel
    {
        public string? Adres { get; set; }
        public string? AppWachtwoord { get; set; }
    }

    private readonly IDataProtector _beschermer;
    private readonly IConfiguration _configuratie;
    private readonly ILogger<GmailGeheimen> _logger;
    private readonly string _bestandPad;
    private readonly object _slot = new();

    private bool _geladen;
    private string? _adres;
    private string? _appWachtwoord;

    public GmailGeheimen(
        IDataProtectionProvider bescherming,
        IConfiguration configuratie,
        IWebHostEnvironment omgeving,
        ILogger<GmailGeheimen> logger)
    {
        _beschermer = bescherming.CreateProtector(BeschermerDoel);
        _configuratie = configuratie;
        _logger = logger;
        _bestandPad = Path.Combine(omgeving.ContentRootPath, "App_Data", Bestandsnaam);
    }

    /// <summary>
    /// Geeft het gekoppelde Gmail-adres en App-wachtwoord terug. Ontbreekt er iets,
    /// dan zijn beide waarden null — er wordt nooit een uitzondering doorgegeven
    /// aan de aanroeper.
    /// </summary>
    public (string? Adres, string? AppWachtwoord) Lezen()
    {
        lock (_slot)
        {
            if (!_geladen)
            {
                Laad();
                _geladen = true;
            }

            // Configuratie als terugvaloptie: handig op een host waar je de
            // gegevens via omgevingsvariabelen wilt instellen in plaats van via de UI.
            if (string.IsNullOrWhiteSpace(_adres) || string.IsNullOrWhiteSpace(_appWachtwoord))
            {
                var adres = _configuratie["Gmail:Adres"]?.Trim();
                var wachtwoord = _configuratie["Gmail:AppWachtwoord"]?.Trim();
                if (!string.IsNullOrWhiteSpace(adres) && !string.IsNullOrWhiteSpace(wachtwoord))
                {
                    return (adres, wachtwoord);
                }
            }

            return (_adres, _appWachtwoord);
        }
    }

    /// <summary>
    /// Slaat de gekoppelde Gmail-gegevens versleuteld op.
    /// </summary>
    public void Opslaan(string? adres, string? appWachtwoord)
    {
        if (string.IsNullOrWhiteSpace(adres))
        {
            throw new ArgumentException("Het Gmail-adres mag niet leeg zijn.", nameof(adres));
        }

        if (string.IsNullOrWhiteSpace(appWachtwoord))
        {
            throw new ArgumentException("Het App-wachtwoord mag niet leeg zijn.", nameof(appWachtwoord));
        }

        // Gmail toont App-wachtwoorden met spaties; die horen niet bij het wachtwoord.
        var genormaliseerdWachtwoord = appWachtwoord.Replace(" ", string.Empty).Trim();
        var genormaliseerdAdres = adres.Trim();

        lock (_slot)
        {
            try
            {
                var opslag = new OpslagModel
                {
                    Adres = _beschermer.Protect(genormaliseerdAdres),
                    AppWachtwoord = _beschermer.Protect(genormaliseerdWachtwoord)
                };

                var map = Path.GetDirectoryName(_bestandPad)!;
                Directory.CreateDirectory(map);

                // Eerst naar een tijdelijk bestand schrijven, dan verplaatsen: een
                // onderbroken schrijfactie laat nooit een half bestand achter.
                var tijdelijk = _bestandPad + ".tmp";
                File.WriteAllText(tijdelijk, JsonSerializer.Serialize(opslag));
                File.Move(tijdelijk, _bestandPad, overwrite: true);

                _adres = genormaliseerdAdres;
                _appWachtwoord = genormaliseerdWachtwoord;
                _geladen = true;

                _logger.LogInformation("Gmail-koppeling opgeslagen voor {Adres}.", genormaliseerdAdres);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "De Gmail-koppeling kon niet versleuteld worden opgeslagen.");
                throw new InvalidOperationException(
                    "De Gmail-koppeling kon niet worden opgeslagen. Controleer of de map App_Data " +
                    "beschrijfbaar is voor de website.", ex);
            }
        }
    }

    /// <summary>
    /// Verwijdert de opgeslagen Gmail-koppeling.
    /// </summary>
    public void Verwijderen()
    {
        lock (_slot)
        {
            try
            {
                if (File.Exists(_bestandPad))
                {
                    File.Delete(_bestandPad);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Het bestand met Gmail-gegevens kon niet worden verwijderd.");
            }

            _adres = null;
            _appWachtwoord = null;
            _geladen = true;
        }
    }

    /// <summary>
    /// Geeft terug of er een Gmail-adres én App-wachtwoord beschikbaar zijn.
    /// </summary>
    public bool IsGekoppeld
    {
        get
        {
            var (adres, wachtwoord) = Lezen();
            return !string.IsNullOrWhiteSpace(adres) && !string.IsNullOrWhiteSpace(wachtwoord);
        }
    }

    private void Laad()
    {
        if (!File.Exists(_bestandPad))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_bestandPad);
            var opslag = JsonSerializer.Deserialize<OpslagModel>(json);

            if (!string.IsNullOrWhiteSpace(opslag?.Adres))
            {
                _adres = _beschermer.Unprotect(opslag.Adres);
            }

            if (!string.IsNullOrWhiteSpace(opslag?.AppWachtwoord))
            {
                _appWachtwoord = _beschermer.Unprotect(opslag.AppWachtwoord);
            }
        }
        catch (Exception ex)
        {
            // Bijvoorbeeld wanneer de Data Protection-sleutels verloren zijn gegaan.
            _logger.LogWarning(ex,
                "De opgeslagen Gmail-gegevens konden niet worden ontsleuteld. Koppel het Gmail-adres opnieuw via /admin/email.");
            _adres = null;
            _appWachtwoord = null;
        }
    }
}
