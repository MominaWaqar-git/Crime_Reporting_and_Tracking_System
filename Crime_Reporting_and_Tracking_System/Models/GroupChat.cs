using System;
using System.ComponentModel.DataAnnotations;

namespace Crime_Reporting_and_Tracking_System.Models
{
    public class GroupChat
    {
        [Key]
        public int ChatId { get; set; }
        public int ComplaintId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;
    }
}