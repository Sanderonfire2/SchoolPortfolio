using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace SchoolPortfolio.Services;

/// <summary>
/// Beheert de e-mailinstellingen: het afzenderadres op het eigen domein en de
/// inloggegevens van de Brevo SMTP-relay (SMTP-login + SMTP-key).
///
/// De gegevens staan nooit in appsettings.json of in de repository. Ze worden
/// versleuteld met ASP.NET Core Data Protection bewaard in
/// <c>App_Data/email-geheimen.json</c>. Die map staat in .gitignore en blijft op
/// de server bewaard, zodat de koppeling een herstart overleeft.
///
/// Als er (nog) geen koppeling is, worden per ontbrekend veld de
/// configuratiesleutels <c>Email:Afzender</c>, <c>Email:SmtpGebruiker</c> en
/// <c>Email:SmtpWachtwoord</c> gebruikt (omgevingsvariabelen
/// <c>Email__Afzender</c>, <c>Email__SmtpGebruiker</c> en
/// <c>Email__SmtpWachtwoord</c>). Handig op een host waar je de gegevens via de
/// omgeving wilt instellen in plaats van via /admin/email.
/// </summary>
public sealed class EmailGeheimen
{
    private const string Bestandsnaam = "email-geheimen.json";
    private const string BeschermerDoel = "EmailGeheimen";

    private sealed class OpslagModel
    {
        public string? Afzender { get; set; }
        public string? SmtpGebruiker { get; set; }
        public string? SmtpWachtwoord { get; set; }
    }

    private readonly IDataProtector _beschermer;
    private readonly IConfiguration _configuratie;
    private readonly ILogger<EmailGeheimen> _logger;
    private readonly string _bestandPad;
    private readonly object _slot = new();

    private bool _geladen;
    private string? _afzender;
    private string? _smtpGebruiker;
    private string? _smtpWachtwoord;

    public EmailGeheimen(
        IDataProtectionProvider bescherming,
        IConfiguration configuratie,
        IWebHostEnvironment omgeving,
        ILogger<EmailGeheimen> logger)
    {
        _beschermer = bescherming.CreateProtector(BeschermerDoel);
        _configuratie = configuratie;
        _logger = logger;
        _bestandPad = Path.Combine(omgeving.ContentRootPath, "App_Data", Bestandsnaam);
    }

    /// <summary>
    /// Geeft het afzenderadres, de SMTP-login en de SMTP-key terug. Ontbreekt er
    /// iets, dan is dat veld null — er wordt nooit een uitzondering doorgegeven
    /// aan de aanroeper.
    /// </summary>
    public (string? Afzender, string? SmtpGebruiker, string? SmtpWachtwoord) Lezen()
    {
        lock (_slot)
        {
            if (!_geladen)
            {
                Laad();
                _geladen = true;
            }

            // Per veld terugvallen op de configuratie, zodat je de waarden ook via
            // omgevingsvariabelen kunt instellen.
            var afzender = string.IsNullOrWhiteSpace(_afzender)
                ? _configuratie["Email:Afzender"]?.Trim()
                : _afzender;
            var gebruiker = string.IsNullOrWhiteSpace(_smtpGebruiker)
                ? _configuratie["Email:SmtpGebruiker"]?.Trim()
                : _smtpGebruiker;
            var wachtwoord = string.IsNullOrWhiteSpace(_smtpWachtwoord)
                ? _configuratie["Email:SmtpWachtwoord"]?.Trim()
                : _smtpWachtwoord;

            return (afzender, gebruiker, wachtwoord);
        }
    }

    /// <summary>
    /// Slaat de e-mailinstellingen versleuteld op. Een lege SMTP-key laat de
    /// bestaande key staan (zodat je alleen het adres kunt wijzigen).
    /// </summary>
    public void Opslaan(string afzender, string smtpGebruiker, string? smtpWachtwoord)
    {
        if (string.IsNullOrWhiteSpace(afzender))
        {
            throw new ArgumentException("Het afzenderadres mag niet leeg zijn.", nameof(afzender));
        }

        if (string.IsNullOrWhiteSpace(smtpGebruiker))
        {
            throw new ArgumentException("De SMTP-login mag niet leeg zijn.", nameof(smtpGebruiker));
        }

        var genormaliseerdeAfzender = afzender.Trim();
        var genormaliseerdeGebruiker = smtpGebruiker.Trim();
        var genormaliseerdWachtwoord = string.IsNullOrWhiteSpace(smtpWachtwoord)
            ? null
            : smtpWachtwoord.Trim();

        lock (_slot)
        {
            if (!_geladen)
            {
                Laad();
                _geladen = true;
            }

            if (genormaliseerdWachtwoord is null)
            {
                genormaliseerdWachtwoord = _smtpWachtwoord;
            }

            if (string.IsNullOrWhiteSpace(genormaliseerdWachtwoord))
            {
                throw new ArgumentException(
                    "De SMTP-key mag niet leeg zijn.", nameof(smtpWachtwoord));
            }

            try
            {
                var opslag = new OpslagModel
                {
                    Afzender = _beschermer.Protect(genormaliseerdeAfzender),
                    SmtpGebruiker = _beschermer.Protect(genormaliseerdeGebruiker),
                    SmtpWachtwoord = _beschermer.Protect(genormaliseerdWachtwoord)
                };

                var map = Path.GetDirectoryName(_bestandPad)!;
                Directory.CreateDirectory(map);

                // Eerst naar een tijdelijk bestand schrijven, dan verplaatsen: een
                // onderbroken schrijfactie laat nooit een half bestand achter.
                var tijdelijk = _bestandPad + ".tmp";
                File.WriteAllText(tijdelijk, JsonSerializer.Serialize(opslag));
                File.Move(tijdelijk, _bestandPad, overwrite: true);

                _afzender = genormaliseerdeAfzender;
                _smtpGebruiker = genormaliseerdeGebruiker;
                _smtpWachtwoord = genormaliseerdWachtwoord;
                _geladen = true;

                _logger.LogInformation("E-mailinstellingen opgeslagen voor {Afzender} (Brevo SMTP).", genormaliseerdeAfzender);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "De e-mailinstellingen konden niet versleuteld worden opgeslagen.");
                throw new InvalidOperationException(
                    "De e-mailinstellingen konden niet worden opgeslagen. Controleer of de map App_Data " +
                    "beschrijfbaar is voor de website.", ex);
            }
        }
    }

    /// <summary>
    /// Verwijdert de opgeslagen e-mailinstellingen.
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
                _logger.LogWarning(ex, "Het bestand met e-mailinstellingen kon niet worden verwijderd.");
            }

            _afzender = null;
            _smtpGebruiker = null;
            _smtpWachtwoord = null;
            _geladen = true;
        }
    }

    /// <summary>
    /// Geeft terug of afzender, SMTP-login en SMTP-key alle drie beschikbaar zijn.
    /// </summary>
    public bool IsGekoppeld
    {
        get
        {
            var (afzender, gebruiker, wachtwoord) = Lezen();
            return !string.IsNullOrWhiteSpace(afzender)
                && !string.IsNullOrWhiteSpace(gebruiker)
                && !string.IsNullOrWhiteSpace(wachtwoord);
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

            if (!string.IsNullOrWhiteSpace(opslag?.Afzender))
            {
                _afzender = _beschermer.Unprotect(opslag.Afzender);
            }

            if (!string.IsNullOrWhiteSpace(opslag?.SmtpGebruiker))
            {
                _smtpGebruiker = _beschermer.Unprotect(opslag.SmtpGebruiker);
            }

            if (!string.IsNullOrWhiteSpace(opslag?.SmtpWachtwoord))
            {
                _smtpWachtwoord = _beschermer.Unprotect(opslag.SmtpWachtwoord);
            }
        }
        catch (Exception ex)
        {
            // Bijvoorbeeld wanneer de Data Protection-sleutels verloren zijn gegaan.
            _logger.LogWarning(ex,
                "De opgeslagen e-mailinstellingen konden niet worden ontsleuteld. Stel ze opnieuw in via /admin/email.");
            _afzender = null;
            _smtpGebruiker = null;
            _smtpWachtwoord = null;
        }
    }
}
