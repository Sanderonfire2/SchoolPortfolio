using Microsoft.EntityFrameworkCore;
using SchoolPortfolio.Models;

namespace SchoolPortfolio.Data;

public class SiteDbContext(DbContextOptions<SiteDbContext> options) : DbContext(options)
{
    public DbSet<Profiel> Profielen { get; set; }
    public DbSet<Project> Projecten { get; set; }
    public DbSet<GallerijAfbeelding> GallerijAfbeeldingen { get; set; }
    public DbSet<Opleiding> Opleidingen { get; set; }
    public DbSet<Stage> Stages { get; set; }
    public DbSet<Rapport> Rapporten { get; set; }
    public DbSet<PovLocatie> PovLocaties { get; set; }
    public DbSet<Werkervaring> Werkervaringen { get; set; }
    public DbSet<PaginaInhoud> PaginaInhouden { get; set; }
    public DbSet<ContactBericht> ContactBerichten { get; set; }
    public DbSet<FaqItem> FaqItems { get; set; }
    public DbSet<DownloadItem> DownloadItems { get; set; }
    public DbSet<AreiBoek> AreiBoeken { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaginaInhoud>().HasKey(p => p.Sleutel);
        modelBuilder.Entity<Project>()
            .HasMany(p => p.Afbeeldingen)
            .WithOne()
            .HasForeignKey(a => a.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Rapport>().HasIndex(r => r.Datum);
        modelBuilder.Entity<Rapport>().HasIndex(r => r.StageId);
        modelBuilder.Entity<Rapport>().HasIndex(r => r.PovLocatieId);
        modelBuilder.Entity<AreiBoek>().HasIndex(b => b.Slug).IsUnique();
    }
}
