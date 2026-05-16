using Microsoft.EntityFrameworkCore;
using CrimeReportingSystem.Models;

namespace Crime_Reporting_and_Tracking_System.Data
{
    public class ApplicationDbContext : DbContext
    {
        // Constructor: Iske zariye connection string Program.cs se pass hoti hai
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSset aapki 'Officers' table ko C# mein represent karta hai
        public DbSet<Officer> Officers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Agar aap database mein table ka naam custom rakhna chahein (Optional)
            modelBuilder.Entity<Officer>().ToTable("Officers");
        }
    }
}