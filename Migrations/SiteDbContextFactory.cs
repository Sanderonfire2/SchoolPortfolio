using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SchoolPortfolio.Data;

namespace SchoolPortfolio.Migrations
{
    public class SiteDbContextFactory : IDesignTimeDbContextFactory<SiteDbContext>
    {
        public SiteDbContext CreateDbContext(string[] args)
        {
            var opties = new DbContextOptionsBuilder<SiteDbContext>()
                .UseSqlite("Data Source=app.db")
                .Options;
            return new SiteDbContext(opties);
        }
    }
}
