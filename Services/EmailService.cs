using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;
using SchoolPortfolio.Data;
using SchoolPortfolio.Models;

namespace SchoolPortfolio.Services;

/// <summary>
/// Verstuurde transmissiemails (contactbevestiging aan bezoeker, notificatie aan
/// beheerder bij een nieuw bericht) met als centrale afzender het e-mailadres dat
/// in het Profiel-record staat — dus één instelling in /admin/profiel bepaalt alle
/// verzuurders.
///
/// Verzending gebeurt via Gmail SMTP (smtp.gmail.com, poort 587, STARTTLS) met een
/// Google App-wachtwoord. De gekoppelde Gmail-adres en het App-wachtwoord worden
/// niet in appsettings.json of in de repository opgeslagen; ze worden versleuteld
/// opgeslagen via de GmailGeheimen-klasse (ASP.NET Core Data Protection).
///
/// Als er geen Gmail-adres is gekoppeld, vindt er geen verzending plaats en wordt
/// de fout helder gelogd.
/// </summary>
public class EmailService(SiteDbContext db, ILogger<EmailService> logger, GmailGeheimen gmailGeheimen)
{
    private string? AfzenderAdres => db.Profielen.FirstOrDefault()?.Email?.Trim();

    private string AfzenderWeergave =>
        db.Profielen.FirstOrDefault()?.VolledigeNaam?.Trim() ?? "Sander Van Praet";

    /// <summary>
    /// Stuur een bevestiging naar de bezoeker die een contactbericht heeft ingediend.
    /// </summary>
    public async Task VerstuurBevestigingAanBezoekerAsync(ContactBericht bericht)
    {
        if (string.IsNullOrWhiteSpace(AfzenderAdres))
        {
            logger.LogWarning("Geen afzenderadres geconfigureerd; bevestigingsmail naar bezoeker wordt overgeslagen (ContactBericht {Id}).", bericht.Id);
            return;
        }

        try
        {
            await VerstuurAsync(
                new MailBericht
                {
                    Aan = bericht.Email,
                    Van = AfzenderAdres,
                    Velden = new Dictionary<string, string>
                    {
                        ["naam"] = bericht.Naam,
                        ["onderwerp"] = bericht.Onderwerp
                    },
                    Onderwerp = $"Bevestiging: je bericht “{bericht.Onderwerp}” is ontvangen",
                    Sjabloon = "contact-bevestiging"
                });
            logger.LogInformation("Bevestigingsmail verzonden naar {Email} na bericht {Id}.", bericht.Email, bericht.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Bevestigingsmail naar {Email} niet verzonden (bericht {Id}).", bericht.Email, bericht.Id);
        }
    }

    /// <summary>
    /// Stuur een notificatie naar de site-eigenaar dat er een nieuw contactbericht is.
    /// </summary>
    public async Task VerstuurNotificatieAanBeheerderAsync(ContactBericht bericht)
    {
        if (string.IsNullOrWhiteSpace(AfzenderAdres))
        {
            logger.LogWarning("Geen afzenderadres geconfigureerd; notificatie aan beheerder overgeslagen (ContactBericht {Id}).", bericht.Id);
            return;
        }

        try
        {
            await VerstuurAsync(
                new MailBericht
                {
                    Aan = AfzenderAdres,
                    Van = AfzenderAdres,
                    Onderwerp = $"Nieuw contactbericht van {bericht.Naam}: “{bericht.Onderwerp}”",
                    Sjabloon = "contact-notificatie-beheerder",
                    Velden = new Dictionary<string, string>
                    {
                        ["naam"] = bericht.Naam,
                        ["email"] = bericht.Email,
                        ["onderwerp"] = bericht.Onderwerp,
                        ["inhoud"] = bericht.Inhoud
                    }
                });
            logger.LogInformation("Notificatie aan beheerder verzonden naar {Adres} na bericht {Id}.", AfzenderAdres, bericht.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notificatie aan beheerder niet verzonden (bericht {Id}).", bericht.Id);
        }
    }

    /// <summary>
    /// Zelfde notificatie, maar met expliciete toegangsbevestiging dat de beheerder is ingelogd.
    /// </summary>
    public async Task VerstuurBevestigingAanBeheerderAsync(string beheerderNaam, string berichtNaam, string berichtOnderwerp)
    {
        if (string.IsNullOrWhiteSpace(AfzenderAdres))
        {
            logger.LogWarning("Geen afzenderadres geconfigureerd; bevestigingsmail aan beheerder overgeslagen.");
            return;
        }

        try
        {
            await VerstuurAsync(
                new MailBericht
                {
                    Aan = AfzenderAdres,
                    Van = AfzenderAdres,
                    Onderwerp = $"Ingelogd als {beheerderNaam} — bevestiging",
                    Sjabloon = "beheerder-inlogbevestiging",
                    Velden = new Dictionary<string, string>
                    {
                        ["naam"] = beheerderNaam,
                        ["tijd"] = DateTime.Now.ToString("dd MMMM yyyy HH:mm"),
                        ["berichtVan"] = berichtNaam,
                        ["berichtOnderwerp"] = berichtOnderwerp
                    }
                });
            logger.LogInformation("Inlogbevestigingsmail verzonden naar {Adres}.", AfzenderAdres);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Inlogbevestigingsmail niet verzonden.");
        }
    }

    /// <summary>
    /// Verstuurt een echte testmail en gooit de fout van de SMTP-server door naar de
    /// aanroeper, zodat de beheerder in de UI ziet wat er misgaat (verkeerd
    /// App-wachtwoord, geen verbinding, ...).
    /// </summary>
    public async Task VerstuurTestmailAsync(string naar)
    {
        var (gmailAdres, _) = gmailGeheimen.Lezen();
        if (string.IsNullOrWhiteSpace(gmailAdres))
        {
            throw new InvalidOperationException(
                "Er is nog geen Gmail-adres gekoppeld. Koppel eerst een Gmail-adres met App-wachtwoord.");
        }

        await VerstuurAsync(new MailBericht
        {
            Aan = naar,
            Van = gmailAdres,
            Onderwerp = "Testmail vanuit het e-mailbeheer",
            Sjabloon = "testmail",
            Velden = new Dictionary<string, string>
            {
                ["tijd"] = DateTime.Now.ToString("dd MMMM yyyy HH:mm")
            }
        });
    }

    private async Task VerstuurAsync(MailBericht bericht)
    {
        var (smtpGebruiker, smtpWachtwoord) = gmailGeheimen.Lezen();

        if (string.IsNullOrWhiteSpace(smtpGebruiker) || string.IsNullOrWhiteSpace(smtpWachtwoord))
        {
            throw new InvalidOperationException(
                "Geen Gmail-adres of App-wachtwoord is gekoppeld. Koppel eerst een Gmail-adres via /admin/email.");
        }

        if (string.IsNullOrWhiteSpace(bericht.Aan))
        {
            throw new InvalidOperationException("Er is geen ontvangeradres ingesteld voor deze e-mail.");
        }

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Verbinding met Gmail SMTP-server (smtp.gmail.com:587) mislukt.");
            throw new InvalidOperationException(
                $"Verbinding met smtp.gmail.com:587 mislukt: {ex.Message}", ex);
        }

        try
        {
            await client.AuthenticateAsync(smtpGebruiker, smtpWachtwoord);
        }
        catch (Exception ex)
        {
            await client.DisconnectAsync(true);
            logger.LogWarning(ex, "Authenticatie bij Gmail SMTP-server mislukt voor gebruiker {Gebruiker}.", smtpGebruiker);

            throw new InvalidOperationException(BepaalAuthFout(ex, smtpGebruiker), ex);
        }

        try
        {
            var mailBericht = NieuwBericht(bericht, smtpGebruiker);
            await client.SendAsync(mailBericht);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Verzending van e-mailbericht mislukt (onderwerp {Onderwerp}).", bericht.Onderwerp);
            throw new InvalidOperationException(BepaalVerzendFout(ex), ex);
        }
        finally
        {
            await client.DisconnectAsync(true);
        }
    }

    private static bool Bevat(string tekst, string fragment) =>
        tekst.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    private static string BepaalVerzendFout(Exception ex)
    {
        var msg = ex.Message;

        // Eerst de fouten die de Gmail-server zelf terugstuurt: die zijn het
        // duidelijkst voor de gebruiker.
        if (Bevat(msg, "535") || Bevat(msg, "5.7.8") || Bevat(msg, "Username and Password not accepted"))
        {
            return "Authenticatie bij Gmail mislukt. Controleer of het App-wachtwoord correct is en of je Gmail-account 2-stapsverificatie heeft ingeschakeld.";
        }

        if (Bevat(msg, "not authenticated") || Bevat(msg, "authentication failed"))
        {
            return "Authenticatie bij Gmail mislukt. Controleer of het App-wachtwoord correct is en of je Gmail-account 2-stapsverificatie heeft ingeschakeld.";
        }

        if (Bevat(msg, "timeout") || Bevat(msg, "timed out"))
        {
            return "Verzending time-out. Controleer je internetverbinding en of Gmail SMTP (smtp.gmail.com:587) bereikbaar is.";
        }

        if (Bevat(msg, "connection refused") || Bevat(msg, "network is unreachable") ||
            Bevat(msg, "no such host") || Bevat(msg, "failed to connect"))
        {
            return "Verbinding met Gmail SMTP-server mislukt. Controleer je internetverbinding en firewall-instellingen.";
        }

        return $"Verzending mislukt: {msg}";
    }

    private static string BepaalAuthFout(Exception ex, string gebruiker)
    {
        // Veelvoorkomende Gmail SMTP-fouten omheen zodat de gebruiker iets doen kan.
        // Let op de volgorde: "disconnected" bevat ook "connect", dus de
        // inlogfouten moeten eerst gecontroleerd worden.
        var msg = ex.Message;

        if (Bevat(msg, "535") || Bevat(msg, "5.7.8") ||
            Bevat(msg, "Username and Password not accepted") || Bevat(msg, "BadCredentials"))
        {
            return $"Authenticatie mislukt voor {gebruiker}. Het App-wachtwoord wordt niet geaccepteerd. " +
                "Controleer of 2-stapsverificatie aanstaat op https://myaccount.google.com/security, " +
                "maak daar een nieuw App-wachtwoord aan en vul dat in bij /admin/email.";
        }

        if (Bevat(msg, "not authenticated") || Bevat(msg, "authentication failed"))
        {
            return "Authenticatie bij Gmail mislukt. Controleer of het App-wachtwoord correct is en of je Gmail-account 2-stapsverificatie heeft ingeschakeld.";
        }

        if (Bevat(msg, "timeout") || Bevat(msg, "timed out"))
        {
            return "Verbinding met Gmail time-out tijdens inloggen. Controleer je internetverbinding.";
        }

        if (Bevat(msg, "connection refused") || Bevat(msg, "network is unreachable") ||
            Bevat(msg, "no such host") || Bevat(msg, "failed to connect"))
        {
            return "Verbinding met Gmail SMTP-server mislukt tijdens inloggen. Controleer je internetverbinding en firewall-instellingen.";
        }

        return $"Authenticatie bij Gmail mislukt voor {gebruiker}: {msg}";
    }

    private MimeMessage NieuwBericht(MailBericht bericht, string afzenderAdres)
    {
        var message = new MimeMessage();
        // Gmail laat alleen mail toe van het account waarmee je inlogt (of een
        // geverifieerde alias). Daarom is de afzender altijd het gekoppelde
        // Gmail-adres, met de naam uit het profiel als weergavenaam.
        message.From.Add(new MailboxAddress(AfzenderWeergave, afzenderAdres));
        message.To.Add(new MailboxAddress("", bericht.Aan));
        message.Subject = bericht.Onderwerp;

        var body = SjabloonTekst(bericht.Sjabloon, bericht.Velden);
        message.Body = new TextPart("plain") { Text = body };

        return message;
    }

    private static string SjabloonTekst(string sjabloon, Dictionary<string, string> velden)
    {
        return sjabloon switch
        {
            "contact-bevestiging" => $@"Beste {velden["naam"]},

dankjewel voor je bericht over “{velden["onderwerp"]}”. We hebben je bericht ontvangen en zullen zo spoedig mogelijk reageren.

Met vriendelijke groeten,
Afdeling Schoolportfolio",

            "contact-notificatie-beheerder" => $@"Nieuw contactbericht van {velden["naam"]} <{velden["email"]}>

Onderwerp: {velden["onderwerp"]}

Bericht:
{velden["inhoud"]}",

            "testmail" => $@"Dit is een testmail van de beheeromgeving van je schoolportfolio.

Verzonden op: {velden["tijd"]}

Als je dit leest, werkt de Gmail-koppeling: e-mails worden echt verstuurd van je eigen Gmail-adres.",

            "beheerder-inlogbevestiging" => $@"Inlogbevestiging voor {velden["naam"]}

Je bent ingelogd op de beheeromgeving van de schoolportfolio.
Tijdstip: {velden["tijd"]}

Laatste actieve bericht:
Van: {velden["berichtVan"]}
Onderwerp: {velden["berichtOnderwerp"]}",

            _ => throw new InvalidOperationException($"Onbekend e-mailsjabloon: {sjabloon}")
        };
    }
}

/// <summary>
/// Een e-mailbericht dat door EmailService wordt verzonden.
/// </summary>
public sealed class MailBericht
{
    public required string Aan { get; init; }
    public required string Van { get; init; }
    public required string Onderwerp { get; init; }
    public required string Sjabloon { get; init; }
    public Dictionary<string, string> Velden { get; init; } = new();
}
