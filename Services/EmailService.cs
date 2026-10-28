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
/// Verstuurt transactionele mails (contactbevestiging aan bezoeker, notificatie aan
/// beheerder bij een nieuw bericht) via de gratis SMTP-relay van Brevo.
///
/// Afzender is altijd het eigen adres dat bij /admin/email is ingesteld (moet bij
/// Brevo als sender geverifieerd zijn); de weergavenaam komt uit het Profiel-record.
/// De SMTP-login en SMTP-key van Brevo worden versleuteld bewaard via
/// <see cref="EmailGeheimen"/> (ASP.NET Core Data Protection) en staan dus nooit in
/// appsettings.json of in de repository.
///
/// Zonder koppeling wordt er niet verstuurd en wordt de oorzaak helder gelogd.
/// </summary>
public class EmailService(SiteDbContext db, ILogger<EmailService> logger, EmailGeheimen emailGeheimen, IWebHostEnvironment omgeving)
{
    private const string SmtpServer = "smtp-relay.brevo.com";
    private const int SmtpPoort = 587;

    private string AfzenderWeergave =>
        db.Profielen.FirstOrDefault()?.VolledigeNaam?.Trim() ?? "Sander Van Praet";

    /// <summary>
    /// Stuur een bevestiging naar de bezoeker die een contactbericht heeft ingediend.
    /// </summary>
    public async Task VerstuurBevestigingAanBezoekerAsync(ContactBericht bericht)
    {
        try
        {
            await VerstuurAsync(
                new MailBericht
                {
                    Aan = bericht.Email,
                    Onderwerp = $"Bevestiging: je bericht “{KopVeilig(bericht.Onderwerp)}” is ontvangen",
                    Sjabloon = "contact-bevestiging",
                    Velden = new Dictionary<string, string>
                    {
                        ["naam"] = bericht.Naam,
                        ["onderwerp"] = bericht.Onderwerp
                    }
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
    /// Antwoorden op die mail gaan rechtstreeks naar de bezoeker.
    /// </summary>
    public async Task VerstuurNotificatieAanBeheerderAsync(ContactBericht bericht)
    {
        try
        {
            await VerstuurAsync(
                new MailBericht
                {
                    Aan = AfzenderAdres(),
                    AntwoordAan = bericht.Email,
                    Onderwerp = $"Nieuw contactbericht van {KopVeilig(bericht.Naam, 60)}: “{KopVeilig(bericht.Onderwerp)}”",
                    Sjabloon = "contact-notificatie-beheerder",
                    Velden = new Dictionary<string, string>
                    {
                        ["naam"] = bericht.Naam,
                        ["email"] = bericht.Email,
                        ["onderwerp"] = bericht.Onderwerp,
                        ["inhoud"] = bericht.Inhoud,
                        ["tijd"] = bericht.VerstuurdOp.ToString("dd-MM-yyyy HH:mm")
                    }
                });
            logger.LogInformation("Notificatie aan beheerder verzonden naar {Adres} na bericht {Id}.", AfzenderAdres(), bericht.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notificatie aan beheerder niet verzonden (bericht {Id}).", bericht.Id);
        }
    }

    /// <summary>
    /// Bevestiging aan de beheerder dat hij is ingelogd.
    /// </summary>
    public async Task VerstuurBevestigingAanBeheerderAsync(string beheerderNaam, string berichtNaam, string berichtOnderwerp)
    {
        try
        {
            await VerstuurAsync(
                new MailBericht
                {
                    Aan = AfzenderAdres(),
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
            logger.LogInformation("Inlogbevestigingsmail verzonden naar {Adres}.", AfzenderAdres());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Inlogbevestigingsmail niet verzonden.");
        }
    }

    /// <summary>
    /// Verstuurt een echte testmail naar het ingestelde afzenderadres en gooit de fout
    /// van de SMTP-server door naar de aanroeper, zodat de beheerder in de UI ziet wat
    /// er misgaat (verkeerde SMTP-key, niet-geverifieerde afzender, geen verbinding, ...).
    /// </summary>
    public async Task VerstuurTestmailAsync(string naar)
    {
        var (afzender, smtpGebruiker, smtpWachtwoord) = emailGeheimen.Lezen();
        if (string.IsNullOrWhiteSpace(afzender) || string.IsNullOrWhiteSpace(smtpGebruiker) || string.IsNullOrWhiteSpace(smtpWachtwoord))
        {
            throw new InvalidOperationException(
                "De e-mailkoppeling is niet compleet. Stel eerst het afzenderadres, de Brevo SMTP-login en de SMTP-key in via /admin/email.");
        }

        await VerstuurAsync(new MailBericht
        {
            Aan = naar,
            Onderwerp = "Testmail vanuit het e-mailbeheer",
            Sjabloon = "testmail",
            Velden = new Dictionary<string, string>
            {
                ["tijd"] = DateTime.Now.ToString("dd-MM-yyyy HH:mm"),
                ["afzender"] = naar,
                ["server"] = $"{SmtpServer}:{SmtpPoort}"
            }
        });
    }

    /// <summary>
    /// Maakt een stukje gebruikersinvoer veilig voor een e-mailkop: geen regeleinden
    /// (kop-injectie) en een redelijke maximumlengte.
    /// </summary>
    private static string KopVeilig(string? waarde, int maximum = 120)
    {
        if (string.IsNullOrWhiteSpace(waarde)) return string.Empty;

        var schoon = waarde.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        return schoon.Length <= maximum ? schoon : schoon[..maximum].TrimEnd() + "…";
    }

    private string AfzenderAdres()
    {
        var (afzender, _, _) = emailGeheimen.Lezen();
        return string.IsNullOrWhiteSpace(afzender)
            ? db.Profielen.FirstOrDefault()?.Email?.Trim() ?? string.Empty
            : afzender;
    }

    private async Task VerstuurAsync(MailBericht bericht)
    {
        var (afzender, smtpGebruiker, smtpWachtwoord) = emailGeheimen.Lezen();

        if (string.IsNullOrWhiteSpace(afzender) || string.IsNullOrWhiteSpace(smtpGebruiker) || string.IsNullOrWhiteSpace(smtpWachtwoord))
        {
            throw new InvalidOperationException(
                "De e-mailkoppeling is niet compleet. Stel het afzenderadres, de Brevo SMTP-login en de SMTP-key in via /admin/email.");
        }

        if (string.IsNullOrWhiteSpace(bericht.Aan))
        {
            throw new InvalidOperationException("Er is geen ontvangeradres ingesteld voor deze e-mail.");
        }

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(SmtpServer, SmtpPoort, SecureSocketOptions.StartTls);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Verbinding met Brevo SMTP-relay ({Server}:{Poort}) mislukt.", SmtpServer, SmtpPoort);
            throw new InvalidOperationException(
                $"Verbinding met {SmtpServer}:{SmtpPoort} mislukt: {ex.Message}", ex);
        }

        try
        {
            await client.AuthenticateAsync(smtpGebruiker, smtpWachtwoord);
        }
        catch (Exception ex)
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true);
            }

            logger.LogWarning(ex, "Authenticatie bij Brevo SMTP mislukt voor gebruiker {Gebruiker}.", smtpGebruiker);
            throw new InvalidOperationException(BepaalAuthFout(ex, smtpGebruiker), ex);
        }

        try
        {
            var mailBericht = NieuwBericht(bericht, afzender);
            await client.SendAsync(mailBericht);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Verzending van e-mailbericht mislukt (onderwerp {Onderwerp}).", bericht.Onderwerp);
            throw new InvalidOperationException(BepaalVerzendFout(ex, afzender), ex);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true);
            }
        }
    }

    private static bool Bevat(string tekst, string fragment) =>
        tekst.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    private static string BepaalVerzendFout(Exception ex, string afzender)
    {
        var msg = ex.Message;

        // Fouten die de Brevo-server zelf terugstuurt zijn het duidelijkst.
        if (Bevat(msg, "535") || Bevat(msg, "5.7.8") || Bevat(msg, "authentication failed") ||
            Bevat(msg, "not authenticated") || Bevat(msg, "BadCredentials"))
        {
            return "Inloggen bij de Brevo SMTP-relay mislukt. Controleer de SMTP-login en de SMTP-key " +
                "(Brevo → Settings → SMTP & API → SMTP). Gebruik een SMTP-key, niet je accountwachtwoord " +
                "en niet een API-key.";
        }

        if (Bevat(msg, "sender") || Bevat(msg, "from address") || Bevat(msg, "not verified"))
        {
            return $"Brevo weigert het afzenderadres {afzender}. Voeg dat adres toe en verifieer het bij " +
                "Brevo onder Settings → Senders, Domains & IPs → Senders. Een adres op je eigen domein " +
                "werkt pas nadat het adres (of het hele domein) geverifieerd is.";
        }

        if (Bevat(msg, "not activated") || Bevat(msg, "activate") || Bevat(msg, "transactional"))
        {
            return "Transactional e-mail is nog niet geactiveerd voor dit Brevo-account. Vraag de activatie " +
                "aan via het Brevo-dashboard of de Brevo-support, en probeer daarna opnieuw.";
        }

        if (Bevat(msg, "quota") || Bevat(msg, "daily limit") || Bevat(msg, "too many"))
        {
            return "De daglimiet van je Brevo-abonnement is bereikt (gratis: 300 mails per dag). " +
                "Probeer het morgen opnieuw of upgrades je Brevo-plan.";
        }

        if (Bevat(msg, "timeout") || Bevat(msg, "timed out"))
        {
            return $"Verzending time-out. Controleer je internetverbinding en of {SmtpServer} op poort {SmtpPoort} bereikbaar is.";
        }

        if (Bevat(msg, "connection refused") || Bevat(msg, "network is unreachable") ||
            Bevat(msg, "no such host") || Bevat(msg, "failed to connect"))
        {
            return $"Verbinding met {SmtpServer} mislukt. Controleer je internetverbinding en de firewall van de host.";
        }

        return $"Verzending mislukt: {msg}";
    }

    private static string BepaalAuthFout(Exception ex, string gebruiker)
    {
        // Let op de volgorde: "disconnected" bevat ook "connect", dus de
        // inlogfouten moeten eerst gecontroleerd worden.
        var msg = ex.Message;

        if (Bevat(msg, "535") || Bevat(msg, "5.7.8") || Bevat(msg, "authentication failed") ||
            Bevat(msg, "not authenticated") || Bevat(msg, "BadCredentials"))
        {
            return $"Inloggen bij Brevo mislukt voor {gebruiker}. Controleer de SMTP-login en de SMTP-key: " +
                "Brevo → Settings → SMTP & API → SMTP → Generate a new SMTP key. Gebruik de SMTP-key, " +
                "niet je accountwachtwoord of een API-key, en let op spaties of een afgekapte key.";
        }

        if (Bevat(msg, "timeout") || Bevat(msg, "timed out"))
        {
            return "Verbinding met Brevo time-out tijdens het inloggen. Controleer je internetverbinding.";
        }

        if (Bevat(msg, "connection refused") || Bevat(msg, "network is unreachable") ||
            Bevat(msg, "no such host") || Bevat(msg, "failed to connect"))
        {
            return "Verbinding met Brevo mislukt tijdens het inloggen. Controleer je internetverbinding en de firewall van de host.";
        }

        return $"Inloggen bij Brevo mislukt voor {gebruiker}: {msg}";
    }

    private MimeMessage NieuwBericht(MailBericht bericht, string afzenderAdres)
    {
        var message = new MimeMessage();
        // De afzender is het eigen adres dat in /admin/email is ingesteld en bij
        // Brevo geverifieerd is; de weergavenaam komt uit het profiel.
        message.From.Add(new MailboxAddress(AfzenderWeergave, afzenderAdres));
        message.To.Add(new MailboxAddress("", bericht.Aan));
        message.Subject = bericht.Onderwerp;

        if (!string.IsNullOrWhiteSpace(bericht.AntwoordAan))
        {
            message.ReplyTo.Add(MailboxAddress.Parse(bericht.AntwoordAan));
        }

        // Het logo gaat als inline bijlage mee (Content-ID), zodat de mail geen
        // externe afbeelding hoeft te laden en er ook geen publieke url nodig is.
        var logoPad = Path.Combine(omgeving.WebRootPath ?? "wwwroot", "img", "logo-email.png");
        var logoAanwezig = File.Exists(logoPad);

        var (tekst, html) = EmailTemplate.Bouw(
            bericht.Sjabloon,
            bericht.Velden,
            EmailTemplate.ContextVan(db.Profielen.FirstOrDefault()),
            logoAanwezig);

        // multipart/alternative met een leesbare tekstversie én een HTML-versie;
        // de HTML hangt samen met het logo in multipart/related.
        var alternatief = new MultipartAlternative();
        alternatief.Add(new TextPart("plain") { Text = tekst });

        var gerelateerd = new MultipartRelated();
        gerelateerd.Add(new TextPart("html") { Text = html });

        if (logoAanwezig)
        {
            gerelateerd.Add(new MimePart("image", "png")
            {
                // In het geheugen lezen: de mail wordt pas later verzonden, dus het
                // bestand mag niet open blijven staan.
                Content = new MimeContent(new MemoryStream(File.ReadAllBytes(logoPad))),
                ContentId = EmailTemplate.LogoContentId,
                ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = "logo-sander-van-praet.png"
            });
        }

        alternatief.Add(gerelateerd);
        message.Body = alternatief;
        message.Date = DateTimeOffset.Now;

        return message;
    }

}

/// <summary>
/// Een e-mailbericht dat door EmailService wordt verzonden.
/// </summary>
public sealed class MailBericht
{
    public required string Aan { get; init; }
    public string? AntwoordAan { get; init; }
    public required string Onderwerp { get; init; }
    public required string Sjabloon { get; init; }
    public Dictionary<string, string> Velden { get; init; } = new();
}
