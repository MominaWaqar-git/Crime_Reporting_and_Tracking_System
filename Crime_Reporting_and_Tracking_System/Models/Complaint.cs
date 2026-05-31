using System;
using System.ComponentModel.DataAnnotations;

namespace Crime_Reporting_and_Tracking_System.Models
{
    public class Complaint
    {
        [Key]
        public int ID { get; set; }

        [Required]
        public string CrimeType { get; set; }

        public DateTime IncidentDate { get; set; }

        [Required]
        public string Location { get; set; }

        public string Description { get; set; }

        public string Status { get; set; } = "Pending Approval";

        [Required]
        public string CitizenName { get; set; }

        [Required]
        public string CitizenPhone { get; set; }
    }
}