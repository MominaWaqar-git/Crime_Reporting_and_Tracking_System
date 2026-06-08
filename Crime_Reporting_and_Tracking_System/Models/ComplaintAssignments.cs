using CrimeReportingSystem.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crime_Reporting_and_Tracking_System.Models
{
    public class ComplaintAssignment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ComplaintId { get; set; }

        [Required]
        public int OfficerId { get; set; }

        public DateTime AssignedDate { get; set; } = DateTime.Now;

        public string? AssignedBy { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation Properties
        [ForeignKey("ComplaintId")]
        public virtual Complaint Complaint { get; set; }

        [ForeignKey("OfficerId")]
        public virtual Officer Officer { get; set; }
    }
}