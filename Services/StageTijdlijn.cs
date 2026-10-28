using System.Globalization;
using SchoolPortfolio.Models;

namespace SchoolPortfolio.Services;

/// <summary>Waar de stage zich vandaag bevindt.</summary>
public enum StageFase
{
    GeenDatums,
    Gepland,
    Bezig,
    Afgerond
}

/// <summary>
/// Berekent de aftelklok en de voortgang van een stage uit een start- en einddatum.
///
/// Alles gebeurt op dagprecision (geen uren), zodat de server en de browser
/// dezelfde uitkomst geven. De weergave gebruikt dezelfde gegevens in
/// <c>_StageStatus.cshtml</c>; het javascript ververst alleen de getallen.
/// </summary>
public readonly record struct StageTijdlijn(
    StageFase Fase,
    DateTime? Start,
    DateTime? Eind,
    int DagenTotStart,
    int DagNummer,
    int TotaalDagen,
    int ResterendeDagen,
    double Voortgang)
{
    public bool HeeftDatums => Fase != StageFase.GeenDatums;

    /// <summary>Ware als er een voortgangsbalk getoond kan worden.</summary>
    public bool HeeftVoortgang => Fase is StageFase.Bezig or StageFase.Afgerond && TotaalDagen > 0;

    public static StageTijdlijn Van(DateTime? start, DateTime? eind, DateTime? vandaag = null)
    {
        var dag = (vandaag ?? DateTime.Today).Date;
        var s = start?.Date;
        var e = eind?.Date;

        if (s is null && e is null)
        {
            return new StageTijdlijn(StageFase.GeenDatums, null, null, 0, 0, 0, 0, 0);
        }

        // Een einddatum vóór de startdatum is onbruikbaar: behandel de stage dan
        // als een stage zonder geplande einddatum in plaats van rare getallen te tonen.
        if (s is not null && e is not null && e < s)
        {
            e = null;
        }

        if (s is not null && dag < s.Value)
        {
            var dagen = (s.Value - dag).Days;
            return new StageTijdlijn(StageFase.Gepland, s, e, dagen, 0, 0, 0, 0);
        }

        if (e is not null && dag > e.Value)
        {
            var totaal = s is null ? 0 : (e.Value - s.Value).Days + 1;
            return new StageTijdlijn(StageFase.Afgerond, s, e, 0, totaal, totaal, 0, 1);
        }

        var totaalDagen = s is not null && e is not null ? (e.Value - s.Value).Days + 1 : 0;
        var dagNummer = s is not null ? (dag - s.Value).Days + 1 : 0;
        var resterend = e is not null ? (e.Value - dag).Days : 0;

        var voortgang = totaalDagen > 0
            ? Math.Clamp((double)dagNummer / totaalDagen, 0, 1)
            : 0;

        return new StageTijdlijn(StageFase.Bezig, s, e, 0, dagNummer, totaalDagen, resterend, voortgang);
    }

    /// <summary>
    /// "14 april 2027 — 26 juni 2027", "vanaf 14 april 2027", "tot 26 juni 2027"
    /// of een lege string. Maanden staan in het Nederlands; de applicatie draait
    /// met InvariantGlobalization, dus er wordt niet op de systeemcultuur vertrouwd.
    /// </summary>
    public string PeriodeTekst
    {
        get
        {
            if (Start is null && Eind is null) return string.Empty;
            if (Start is not null && Eind is not null) return $"{DatumAlsTekst(Start.Value)} — {DatumAlsTekst(Eind.Value)}";
            if (Start is not null) return $"vanaf {DatumAlsTekst(Start.Value)}";
            return $"tot {DatumAlsTekst(Eind!.Value)}";
        }
    }

    /// <summary>
    /// Kiest de stage die de aftelklok op de site aandrijft: eerst een stage die
    /// vandaag loopt, dan de eerstvolgende, en anders de laatst afgeronde stage.
    /// Stages zonder datums doen niet mee.
    /// </summary>
    public static Stage? KiesActieveStage(IEnumerable<Stage> stages, DateTime? vandaag = null)
    {
        var dag = (vandaag ?? DateTime.Today).Date;
        var metDatums = stages
            .Where(s => s.StartDatum is not null)
            .OrderBy(s => s.StartDatum)
            .ToList();

        var lopend = metDatums.FirstOrDefault(s =>
            s.StartDatum!.Value.Date <= dag &&
            (s.EindDatum is null || s.EindDatum.Value.Date >= dag));
        if (lopend is not null) return lopend;

        var volgende = metDatums.FirstOrDefault(s => s.StartDatum!.Value.Date > dag);
        if (volgende is not null) return volgende;

        return metDatums.LastOrDefault();
    }

    /// <summary>"14 april 2027" met een Nederlandse maandnaam.</summary>
    public static string DatumAlsTekst(DateTime datum)
    {
        string[] maanden =
        [
            "januari", "februari", "maart", "april", "mei", "juni",
            "juli", "augustus", "september", "oktober", "november", "december"
        ];

        var maand = maanden[Math.Clamp(datum.Month - 1, 0, 11)];
        return string.Create(CultureInfo.InvariantCulture, $"{datum.Day} {maand} {datum.Year}");
    }
}
