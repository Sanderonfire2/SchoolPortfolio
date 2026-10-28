using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SchoolPortfolio.Models;

public class Profiel
{
    public int Id { get; set; }
    public string VolledigeNaam { get; set; } = "";
    public string Tagline { get; set; } = "";
    public string KorteIntroductie { get; set; } = "";
    public string OverMijTekst { get; set; } = "";
    public string? Woonplaats { get; set; }
    public string? Email { get; set; }
    public string? Telefoon { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? ProfielfotoPad { get; set; }
    public DateTime BijgewerktOp { get; set; }
}

public class Project
{
    public int Id { get; set; }
    public string Titel { get; set; } = "";
    public string? Slug { get; set; }
    public string? Categorie { get; set; }

    /// <summary>Schooljaar binnen de schoolcarrière (bijv. "4de jaar").</summary>
    public string? Schooljaar { get; set; }

    /// <summary>Vak binnen dat schooljaar (bijv. "Elektriciteit").</summary>
    public string? Vak { get; set; }

    public string? Periode { get; set; }
    public string? Rol { get; set; }
    public string? Technologieen { get; set; }
    public string KorteSamenvatting { get; set; } = "";
    public string Omschrijving { get; set; } = "";
    public string? Uitdaging { get; set; }
    public string? Geleerd { get; set; }
    public string? CoverPad { get; set; }
    public bool IsUitgelicht { get; set; }
    public bool IsGepubliceerd { get; set; }
    public int Volgorde { get; set; }
    public DateTime BijgewerktOp { get; set; }

    public List<GallerijAfbeelding> Afbeeldingen { get; set; } = new();
}

public class GallerijAfbeelding
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Pad { get; set; } = "";

    /// <summary>Soort media: "afbeelding", "video" of "document" (pdf/tekeningen).</summary>
    public string Soort { get; set; } = "afbeelding";

    public string? Onderschrift { get; set; }
    public bool IsHoofdCover { get; set; }
    public int Volgorde { get; set; }
}

public class Opleiding
{
    public int Id { get; set; }
    public string Periode { get; set; } = "";
    public string Titel { get; set; } = "";
    public string? Instelling { get; set; }
    public string? Locatie { get; set; }
    public string Omschrijving { get; set; } = "";
    public bool IsGepubliceerd { get; set; }
    public int Volgorde { get; set; }
}

public class Stage
{
    public int Id { get; set; }
    public string Periode { get; set; } = "";
    public string Bedrijf { get; set; } = "";
    public string Functie { get; set; } = "";
    public string? Locatie { get; set; }
    public string Omschrijving { get; set; } = "";
    public string? Taken { get; set; }

    /// <summary>Verwijzing naar een oud incidenteel stageverslag; nieuwe verslagen worden Rapport-records.</summary>
    public string? VerslagPad { get; set; }

    /// <summary>Geplande startdatum van de stage; drijft de aftelfunktionaliteit op de startpagina.</summary>
    public DateTime? StartDatum { get; set; }

    /// <summary>Schooljaar binnen de stageperiode ("5de jaar" of "6de jaar").</summary>
    public string? Schooljaar { get; set; }

    public bool IsGepubliceerd { get; set; }
    public int Volgorde { get; set; }

    public List<Rapport> Rapporten { get; set; } = new();
}

public class PovLocatie
{
    public int Id { get; set; }
    public string Naam { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Bedrijf { get; set; }
    public string? Adres { get; set; }
    public string? Periode { get; set; }
    public string Omschrijving { get; set; } = "";
    public bool IsGepubliceerd { get; set; }
    public int Volgorde { get; set; }

    /// <summary>Schooljaar binnen het POV-traject ("5de jaar" of "6de jaar").</summary>
    public string? Schooljaar { get; set; }

    public List<Rapport> Rapporten { get; set; } = new();
}

/// <summary>
/// Dagrapport (PDF) dat bij een stage óf een POV-locatie hoort.
/// Beide categorieën gebruiken dit ene systeem, maar de gegevens blijven gescheiden.
/// </summary>
public class Rapport
{
    public int Id { get; set; }
    public int? StageId { get; set; }
    public int? PovLocatieId { get; set; }
    public string Pad { get; set; } = "";
    public string? Titel { get; set; }
    public DateTime Datum { get; set; } = DateTime.Today;
    public bool IsGepubliceerd { get; set; } = true;
    public int Volgorde { get; set; }
}

public class Werkervaring
{
    public int Id { get; set; }
    public string Periode { get; set; } = "";
    public string Bedrijf { get; set; } = "";
    public string Functie { get; set; } = "";
    public string? Locatie { get; set; }
    public string Omschrijving { get; set; } = "";
    public bool IsGepubliceerd { get; set; }
    public int Volgorde { get; set; }
}

public class PaginaInhoud
{
    [Required]
    public required string Sleutel { get; set; }
    public string Inhoud { get; set; } = "";
    public bool IsGepubliceerd { get; set; }
}

/// <summary>Veelgestelde vraag en antwoord, beheerd via /admin/faq.</summary>
public class FaqItem
{
    public int Id { get; set; }
    public string Vraag { get; set; } = "";
    public string Antwoord { get; set; } = "";
    public bool IsGepubliceerd { get; set; } = true;
    public int Volgorde { get; set; }
}

/// <summary>Een downloadbaar bestand op de downloads pagina, beheerd via /admin/downloads.</summary>
public class DownloadItem
{
    public int Id { get; set; }
    public string Titel { get; set; } = "";
    public string? Omschrijving { get; set; }
    public string BestandPad { get; set; } = "";
    public bool IsGepubliceerd { get; set; } = true;
    public int Volgorde { get; set; }
}

/// <summary>
/// Eén van de drie AREI-boeken. Het bestand staat standaard statisch in
/// /downloaden/arei en kan via /admin/arei worden vervangen door een upload.
/// </summary>
public class AreiBoek
{
    public int Id { get; set; }
    public string Naam { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Beschrijving { get; set; }
    public string? BestandPad { get; set; }
    public int Volgorde { get; set; }
}

public class ContactBericht
{
    public int Id { get; set; }
    public string Naam { get; set; } = "";
    public string Email { get; set; } = "";
    public string Onderwerp { get; set; } = "";
    public string Inhoud { get; set; } = "";
    public DateTime VerstuurdOp { get; set; }
    public bool IsGelezen { get; set; }
    public bool Beantwoord { get; set; }
}

public class ContactFormulier
{
    [Required(ErrorMessage = "Vul je naam in.")]
    public string Naam { get; set; } = "";
    [Required(ErrorMessage = "Vul je e-mailadres in.")]
    [EmailAddress(ErrorMessage = "Dit is geen geldig e-mailadres.")]
    public string Email { get; set; } = "";
    public string Onderwerp { get; set; } = "";
    [Required(ErrorMessage = "Vul je bericht in.")]
    public string Inhoud { get; set; } = "";
}
