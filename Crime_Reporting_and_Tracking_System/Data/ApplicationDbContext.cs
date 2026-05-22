using Microsoft.EntityFrameworkCore;
using CrimeReportingSystem.Models;
using Crime_Reporting_and_Tracking_System.Models;

namespace Crime_Reporting_and_Tracking_System.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Officer> Officers { get; set; }
        public DbSet<PublicAlert> PublicAlerts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Sahi Tareeqa: base ke baad sirf ek dot (.) aur phir method ka naam
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Officer>().ToTable("Officers");
            modelBuilder.Entity<PublicAlert>().ToTable("PublicAlerts");
        }
    }
}