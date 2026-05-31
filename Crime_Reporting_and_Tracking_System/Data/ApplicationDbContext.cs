using Crime_Reporting_and_Tracking_System.Models;
using CrimeReportingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace Crime_Reporting_and_Tracking_System.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Aapke saare tables ke DbSets
        public DbSet<Admin> Admins { get; set; }
        public DbSet<Officer> Officers { get; set; }
        public DbSet<Complaint> Complaints { get; set; }
        public DbSet<PublicAlert> PublicAlerts { get; set; }
        public DbSet<GroupChat> GroupChats { get; set; }

        public DbSet<User> Users { get; set; }
        public DbSet<ChatMessages> ChatMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // SQL Server ki tables ke sath exact mapping
            modelBuilder.Entity<Admin>().ToTable("Admins");
            modelBuilder.Entity<Officer>().ToTable("Officers");
            modelBuilder.Entity<Complaint>().ToTable("Complaints");
            modelBuilder.Entity<PublicAlert>().ToTable("PublicAlerts");
            modelBuilder.Entity<GroupChat>().ToTable("GroupChats");
            modelBuilder.Entity<ChatMessages>().ToTable("ChatMessages");
        }
    }
}