using System.Net;
using System.Text;
using SchoolPortfolio.Models;

namespace SchoolPortfolio.Services;

/// <summary>
/// Bouwt de transactionele e-mails van de site: een strakke HTML-versie in de
/// huisstijl (donkere kop, goud accent, logo) én een leesbaar tekstalternatief
/// voor mailprogramma's die geen HTML tonen.
///
/// De HTML gebruikt uitsluitend tabellen en inline stijlen, omdat veel
/// mailclients geen externe stylesheets of moderne layout ondersteunen. Het logo
/// wordt als bijlage met een Content-ID meegestuurd, zodat er geen externe
/// afbeelding nodig is (en de mail ook offline klopt).
///
/// Alle waarden uit formulieren worden HTML-geëscaleerd voordat ze in de mail
/// belanden: een bezoeker kan dus geen opmaak of links in de beheerder zijn
/// inbox injecteren.
/// </summary>
public static class EmailTemplate
{
    /// <summary>Content-ID waaronder het logo in de HTML-mail wordt meegestuurd.</summary>
    public const string LogoContentId = "logo@schoolportfolio";

    /// <summary>Gegevens uit het profiel die in de mailafsluiting en kop terechtkomen.</summary>
    public sealed record Context(
        string Naam,
        string Tagline,
        string? Email,
        string? Telefoon,
        string? Woonplaats);

    public static Context ContextVan(Profiel? profiel) => new(
        string.IsNullOrWhiteSpace(profiel?.VolledigeNaam) ? "Sander Van Praet" : profiel!.VolledigeNaam.Trim(),
        string.IsNullOrWhiteSpace(profiel?.Tagline) ? "Elektrische installaties & portfolio" : profiel!.Tagline!.Trim(),
        profiel?.Email?.Trim(),
        profiel?.Telefoon?.Trim(),
        profiel?.Woonplaats?.Trim());

    /// <summary>
    /// Geeft (<c>Tekst</c>, <c>Html</c>) voor het opgegeven sjabloon.
    /// </summary>
    public static (string Tekst, string Html) Bouw(
        string sjabloon,
        Dictionary<string, string> velden,
        Context context,
        bool logoAanwezig)
    {
        var (kicker, titel, preheader, inhoud, tekstInhoud, voetnoot) = sjabloon switch
        {
            "contact-bevestiging" => Bevestiging(velden, context),
            "contact-notificatie-beheerder" => Notificatie(velden, context),
            "testmail" => Testmail(velden, context),
            "beheerder-inlogbevestiging" => Inlogbevestiging(velden, context),
            _ => throw new InvalidOperationException($"Onbekend e-mailsjabloon: {sjabloon}")
        };

        var html = Layout(kicker, titel, preheader, inhoud, context, logoAanwezig, voetnoot);
        var tekst = TekstVersie(kicker, titel, tekstInhoud, context, voetnoot);
        return (tekst, html);
    }

    /* ---------- Sjablonen ---------- */

    private static (string Kicker, string Titel, string Preheader, string Inhoud, string TekstInhoud, string Voetnoot)
        Bevestiging(Dictionary<string, string> velden, Context context)
    {
        var naam = Veld(velden, "naam", "bezoeker");
        var onderwerp = Veld(velden, "onderwerp");

        var inhoud = $"""
            <p style="{Paragraaf}">Hallo {E(naam)},</p>
            <p style="{Paragraaf}">Je bericht is goed aangekomen. Ik lees het rustig door en antwoord
            doorgaans binnen één à twee werkdagen.</p>
            {InfoBlok("Je bericht", string.IsNullOrWhiteSpace(onderwerp) ? "Via het contactformulier" : onderwerp)}
            <p style="{Paragraaf}">Wil je nog iets toevoegen? Antwoord dan gewoon op deze e-mail,
            dan komt je aanvulling bij hetzelfde gesprek terecht.</p>
            {Ondertekening(context)}
            """;

        var tekst = $"""
            Hallo {naam},

            Je bericht is goed aangekomen. Ik lees het rustig door en antwoord doorgaans
            binnen één à twee werkdagen.

            Je bericht: {(string.IsNullOrWhiteSpace(onderwerp) ? "via het contactformulier" : onderwerp)}

            Wil je nog iets toevoegen? Antwoord dan gewoon op deze e-mail.
            """;

        return (
            "Bericht ontvangen",
            "Bedankt voor je bericht.",
            "Je bericht is aangekomen — ik antwoord binnen één à twee werkdagen.",
            inhoud,
            tekst,
            "Je ontvangt deze bevestiging omdat je het contactformulier van het portfolio gebruikte.");
    }

    private static (string Kicker, string Titel, string Preheader, string Inhoud, string TekstInhoud, string Voetnoot)
        Notificatie(Dictionary<string, string> velden, Context context)
    {
        var naam = Veld(velden, "naam", "Onbekend");
        var email = Veld(velden, "email");
        var onderwerp = Veld(velden, "onderwerp");
        var inhoudBericht = Veld(velden, "inhoud");
        var tijd = Veld(velden, "tijd", DateTime.Now.ToString("dd-MM-yyyy HH:mm"));

        var mailto = string.IsNullOrWhiteSpace(email) ? null : $"mailto:{Uri.EscapeDataString(email)}";

        var inhoud = $"""
            <p style="{Paragraaf}">Er is een nieuw bericht binnengekomen via het contactformulier.</p>
            {Tabel([
                ("Van", E(naam)),
                ("E-mail", string.IsNullOrWhiteSpace(email) ? "—" : $"<a href=\"mailto:{E(email)}\" style=\"{Link}\">{E(email)}</a>"),
                ("Onderwerp", string.IsNullOrWhiteSpace(onderwerp) ? "<span style=\"color:#8f8d85;\">Geen onderwerp opgegeven</span>" : E(onderwerp)),
                ("Ontvangen", E(tijd))
            ])}
            <div style="margin:24px 0 8px;font-family:{Sans};font-size:11px;letter-spacing:.14em;text-transform:uppercase;color:#8f8d85;">Het bericht</div>
            <div style="{Citaat}">{RegelsNaarHtml(inhoudBericht)}</div>
            {(mailto is null ? string.Empty : Knop("Beantwoorden", mailto))}
            <p style="{Klein}">Antwoorden op deze e-mail gaan rechtstreeks naar {E(naam)}{(string.IsNullOrWhiteSpace(email) ? string.Empty : $" ({E(email)})")}.</p>
            """;

        var tekst = $"""
            Nieuw contactbericht

            Van:        {naam}
            E-mail:     {(string.IsNullOrWhiteSpace(email) ? "—" : email)}
            Onderwerp:  {(string.IsNullOrWhiteSpace(onderwerp) ? "(geen onderwerp)" : onderwerp)}
            Ontvangen:  {tijd}

            Bericht:
            {inhoudBericht}

            Antwoorden op deze e-mail gaan rechtstreeks naar de bezoeker.
            """;

        return (
            "Nieuw contactbericht",
            $"Bericht van {naam}",
            $"{naam} stuurde een bericht via het contactformulier.",
            inhoud,
            tekst,
            "Automatische melding uit de beheeromgeving van het portfolio.");
    }

    private static (string Kicker, string Titel, string Preheader, string Inhoud, string TekstInhoud, string Voetnoot)
        Testmail(Dictionary<string, string> velden, Context context)
    {
        var tijd = Veld(velden, "tijd", DateTime.Now.ToString("dd-MM-yyyy HH:mm"));
        var afzender = Veld(velden, "afzender");
        var server = Veld(velden, "server", "smtp-relay.brevo.com:587");

        var inhoud = $"""
            <p style="{Paragraaf}">Deze testmail is echt verstuurd vanaf je eigen afzenderadres via de
            SMTP-relay van Brevo. Als je hem leest, is de koppeling in orde.</p>
            {Tabel([
                ("Verzonden op", E(tijd)),
                ("Afzender", string.IsNullOrWhiteSpace(afzender) ? "—" : E(afzender)),
                ("SMTP-server", E(server))
            ])}
            <p style="{Paragraaf}">Ziet de mail er goed uit? Dan kunnen bevestigingen aan bezoekers en
            meldingen over nieuwe berichten vanaf nu de deur uit.</p>
            {Ondertekening(context)}
            """;

        var tekst = $"""
            De e-mailkoppeling werkt.

            Deze testmail is echt verstuurd vanaf je eigen afzenderadres via de SMTP-relay
            van Brevo.

            Verzonden op: {tijd}
            Afzender:     {(string.IsNullOrWhiteSpace(afzender) ? "—" : afzender)}
            SMTP-server:  {server}
            """;

        return (
            "E-mailkoppeling",
            "Alles staat klaar.",
            "De testmail is verstuurd — de koppeling met Brevo werkt.",
            inhoud,
            tekst,
            "Automatische testmail uit de beheeromgeving van het portfolio.");
    }

    private static (string Kicker, string Titel, string Preheader, string Inhoud, string TekstInhoud, string Voetnoot)
        Inlogbevestiging(Dictionary<string, string> velden, Context context)
    {
        var naam = Veld(velden, "naam", "beheerder");
        var tijd = Veld(velden, "tijd", DateTime.Now.ToString("dd-MM-yyyy HH:mm"));
        var van = Veld(velden, "berichtVan");
        var onderwerp = Veld(velden, "berichtOnderwerp");

        var inhoud = $"""
            <p style="{Paragraaf}">Er is net ingelogd op de beheeromgeving van je portfolio.</p>
            {Tabel([
                ("Tijdstip", E(tijd)),
                ("Account", E(naam)),
                ("Laatste bericht van", string.IsNullOrWhiteSpace(van) ? "—" : E(van)),
                ("Onderwerp", string.IsNullOrWhiteSpace(onderwerp) ? "—" : E(onderwerp))
            ])}
            <p style="{Klein}">Was jij dit niet? Wijzig dan meteen het beheerderswachtwoord en controleer
            de berichten in het beheer.</p>
            """;

        var tekst = $"""
            Inlogbevestiging

            Tijdstip:             {tijd}
            Account:              {naam}
            Laatste bericht van:  {(string.IsNullOrWhiteSpace(van) ? "—" : van)}
            Onderwerp:            {(string.IsNullOrWhiteSpace(onderwerp) ? "—" : onderwerp)}

            Was jij dit niet? Wijzig dan meteen het beheerderswachtwoord.
            """;

        return (
            "Beheer",
            $"Welkom terug, {naam}.",
            $"Er is net ingelogd op de beheeromgeving ({tijd}).",
            inhoud,
            tekst,
            "Automatische melding uit de beheeromgeving van het portfolio.");
    }

    /* ---------- Opmaak ---------- */

    private const string Sans = "'Segoe UI',-apple-system,BlinkMacSystemFont,Arial,sans-serif";
    private const string Serif = "Georgia,'Times New Roman',serif";
    private const string Paragraaf = $"margin:0 0 16px;font-family:{Sans};font-size:15px;line-height:1.7;color:#45433d;";
    private const string Klein = $"margin:18px 0 0;font-family:{Sans};font-size:12px;line-height:1.6;color:#8f8d85;";
    private const string Link = "color:#8f6a33;text-decoration:underline;";
    private const string Citaat =
        "margin:0;padding:18px 20px;background:#f8f6f1;border-left:3px solid #bd9259;" +
        $"border-radius:0 10px 10px 0;font-family:{Sans};font-size:15px;line-height:1.75;color:#45433d;";

    private static string Layout(
        string kicker,
        string titel,
        string preheader,
        string inhoud,
        Context context,
        bool logoAanwezig,
        string voetnoot)
    {
        var logo = logoAanwezig
            ? $"<img src=\"cid:{LogoContentId}\" width=\"58\" height=\"58\" alt=\"{E(context.Naam)}\" " +
              "style=\"display:block;width:58px;height:58px;border:0;border-radius:50%;\" />"
            : $"<div style=\"width:58px;height:58px;border:1px solid #bd9259;border-radius:50%;" +
              $"font-family:{Serif};font-size:18px;line-height:58px;text-align:center;color:#bd9259;\">SVP</div>";

        return $"""
            <!DOCTYPE html>
            <html lang="nl">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width,initial-scale=1" />
              <meta name="color-scheme" content="light only" />
              <meta name="supported-color-schemes" content="light only" />
              <meta name="x-apple-disable-message-reformatting" />
              <title>{E(titel)}</title>
            </head>
            <body style="margin:0;padding:0;background:#f1eee8;">
              <div style="display:none;max-height:0;max-width:0;overflow:hidden;opacity:0;font-size:1px;line-height:1px;color:#f1eee8;">{E(preheader)}</div>

              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#f1eee8;">
                <tr>
                  <td align="center" style="padding:34px 16px 26px;">
                    <table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" style="width:100%;max-width:600px;border-collapse:separate;">

                      <tr>
                        <td style="background:#131417;border-radius:18px 18px 0 0;padding:26px 30px;">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                            <tr>
                              <td width="58" valign="middle">{logo}</td>
                              <td valign="middle" style="padding-left:16px;">
                                <div style="font-family:{Serif};font-size:20px;line-height:1.2;color:#f6f4ef;">{E(context.Naam)}</div>
                                <div style="padding-top:6px;font-family:{Sans};font-size:10px;letter-spacing:.18em;text-transform:uppercase;color:#bd9259;">{E(context.Tagline)}</div>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>

                      <tr><td style="height:3px;background:#bd9259;font-size:0;line-height:0;">&nbsp;</td></tr>

                      <tr>
                        <td style="background:#ffffff;padding:36px 34px 34px;">
                          <div style="font-family:{Sans};font-size:11px;font-weight:600;letter-spacing:.16em;text-transform:uppercase;color:#8f6a33;padding-bottom:10px;">{E(kicker)}</div>
                          <h1 style="margin:0 0 20px;font-family:{Serif};font-size:27px;line-height:1.25;font-weight:normal;color:#201f1c;">{E(titel)}</h1>
                          {inhoud}
                        </td>
                      </tr>

                      <tr>
                        <td style="background:#131417;border-radius:0 0 18px 18px;padding:22px 30px;">
                          <div style="font-family:{Sans};font-size:12px;line-height:1.65;color:#a09e98;">{E(voetnoot)}</div>
                          <div style="padding-top:10px;font-family:{Sans};font-size:11px;line-height:1.6;color:#777570;">
                            {E(context.Naam)}{(string.IsNullOrWhiteSpace(context.Email) ? string.Empty : $" · <a href=\"mailto:{E(context.Email)}\" style=\"color:#bd9259;text-decoration:none;\">{E(context.Email)}</a>")}
                          </div>
                        </td>
                      </tr>
                    </table>
                    <div style="padding-top:16px;font-family:{Sans};font-size:11px;color:#9a978f;">
                      Portfolio · {E(context.Naam)}
                    </div>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string InfoBlok(string label, string waarde) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:6px 0 20px;border:1px solid #e7e2d8;border-radius:12px;background:#fbfaf7;">
          <tr>
            <td style="padding:16px 18px;">
              <div style="font-family:{Sans};font-size:10px;font-weight:600;letter-spacing:.14em;text-transform:uppercase;color:#8f8d85;">{E(label)}</div>
              <div style="padding-top:5px;font-family:{Serif};font-size:17px;line-height:1.4;color:#201f1c;">{E(waarde)}</div>
            </td>
          </tr>
        </table>
        """;

    private static string Tabel((string Label, string Waarde)[] rijen)
    {
        var bouw = new StringBuilder();
        bouw.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:4px 0 20px;border-collapse:collapse;\">");
        foreach (var (label, waarde) in rijen)
        {
            bouw.Append("<tr>");
            bouw.Append($"<td style=\"padding:10px 0;border-bottom:1px solid #eceae3;font-family:{Sans};font-size:12px;letter-spacing:.04em;text-transform:uppercase;color:#8f8d85;width:38%;vertical-align:top;\">{E(label)}</td>");
            bouw.Append($"<td style=\"padding:10px 0;border-bottom:1px solid #eceae3;font-family:{Sans};font-size:14px;line-height:1.6;color:#201f1c;vertical-align:top;\">{waarde}</td>");
            bouw.Append("</tr>");
        }
        bouw.Append("</table>");
        return bouw.ToString();
    }

    private static string Knop(string tekst, string link) => $"""
        <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:22px 0 4px;">
          <tr>
            <td style="background:#bd9259;border-radius:999px;">
              <a href="{E(link)}" style="display:inline-block;padding:12px 24px;font-family:{Sans};font-size:14px;font-weight:600;color:#17130d;text-decoration:none;border-radius:999px;">{E(tekst)}</a>
            </td>
          </tr>
        </table>
        """;

    private static string Ondertekening(Context context)
    {
        var contact = new List<string>();
        if (!string.IsNullOrWhiteSpace(context.Email)) contact.Add(context.Email);
        if (!string.IsNullOrWhiteSpace(context.Telefoon)) contact.Add(context.Telefoon);
        if (!string.IsNullOrWhiteSpace(context.Woonplaats)) contact.Add(context.Woonplaats);

        var contactRegel = contact.Count == 0
            ? string.Empty
            : $"<div style=\"padding-top:3px;font-family:{Sans};font-size:12px;color:#8f8d85;\">{E(string.Join(" · ", contact))}</div>";

        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:26px 0 0;border-top:1px solid #eceae3;">
              <tr>
                <td style="padding-top:18px;">
                  <div style="font-family:{Serif};font-size:17px;color:#201f1c;">{E(context.Naam)}</div>
                  <div style="padding-top:3px;font-family:{Sans};font-size:12px;color:#8f8d85;">{E(context.Tagline)}</div>
                  {contactRegel}
                </td>
              </tr>
            </table>
            """;
    }

    /* ---------- Tekstversie ---------- */

    private static string TekstVersie(string kicker, string titel, string inhoud, Context context, string voetnoot)
    {
        var lijn = new string('─', 58);
        var bouw = new StringBuilder();
        bouw.AppendLine($"{context.Naam.ToUpperInvariant()} · {context.Tagline.ToUpperInvariant()}");
        bouw.AppendLine(lijn);
        bouw.AppendLine();
        bouw.AppendLine(kicker.ToUpperInvariant());
        bouw.AppendLine(titel);
        bouw.AppendLine();
        bouw.AppendLine(inhoud.Trim());
        bouw.AppendLine();
        bouw.AppendLine(OndertekeningTekst(context));
        bouw.AppendLine();
        bouw.AppendLine(lijn);
        bouw.AppendLine(voetnoot);
        if (!string.IsNullOrWhiteSpace(context.Email))
        {
            bouw.AppendLine($"{context.Naam} · {context.Email}");
        }
        return bouw.ToString();
    }

    private static string OndertekeningTekst(Context context)
    {
        var contact = new List<string>();
        if (!string.IsNullOrWhiteSpace(context.Email)) contact.Add(context.Email);
        if (!string.IsNullOrWhiteSpace(context.Telefoon)) contact.Add(context.Telefoon);
        if (!string.IsNullOrWhiteSpace(context.Woonplaats)) contact.Add(context.Woonplaats);

        var tekst = new StringBuilder();
        tekst.AppendLine("Met vriendelijke groeten,");
        tekst.AppendLine(context.Naam);
        tekst.Append(context.Tagline);
        if (contact.Count > 0)
        {
            tekst.AppendLine();
            tekst.Append(string.Join(" · ", contact));
        }
        return tekst.ToString();
    }

    /* ---------- Hulpjes ---------- */

    private static string Veld(Dictionary<string, string> velden, string sleutel, string standaard = "") =>
        velden.TryGetValue(sleutel, out var waarde) && !string.IsNullOrWhiteSpace(waarde)
            ? waarde.Trim()
            : standaard;

    /// <summary>HTML-escape; nooit rechtstreeks gebruikersinvoer in de mail zetten.</summary>
    private static string E(string? waarde) => WebUtility.HtmlEncode(waarde ?? string.Empty);

    /// <summary>Escalet eerst en zet daarna regeleinden om naar &lt;br /&gt;.</summary>
    private static string RegelsNaarHtml(string? waarde) =>
        E(waarde).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "<br />", StringComparison.Ordinal);
}
