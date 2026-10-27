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
            var optionsBuilder = new DbContextOptionsBuilder<SiteDbContext>();
#pragma warning disable CS0618 // Type or member is obsolete
            optionsBuilder.UseSqlite("Data Source=app.db");
#pragma warning restore CS0618
            return new SiteDbContext(optionsBuilder.Options);
        }
    }
}
