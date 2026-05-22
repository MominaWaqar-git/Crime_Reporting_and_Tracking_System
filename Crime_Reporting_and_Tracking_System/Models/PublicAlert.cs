using System;
using System.ComponentModel.DataAnnotations;

namespace Crime_Reporting_and_Tracking_System.Models
{
    public class PublicAlert
    {
        [Key]
        public int ID { get; set; } 

        [Required(ErrorMessage = "Alert Title is required.")]
        [StringLength(150, ErrorMessage = "Title cannot exceed 150 characters.")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Alert Level is required.")]
        public string AlertLevel { get; set; } // Matches [AlertLevel] from table

        [Required(ErrorMessage = "Location is required.")]
        public string Location { get; set; } // Matches [Location] from table

        public DateTime? DateCreated { get; set; } = DateTime.Now; // Matches [DateCreated] from table

        // Expiry Date (Ke alert kab tak valid rahega)
        [Required(ErrorMessage = "Expiry Date and Time is required.")]
        [DataType(DataType.DateTime)]
        public DateTime ExpiryDate { get; set; }

        // Image Path (Optional - Is liye '?' lagaya hai takay empty reh sakay)
        public string? AttachmentPath { get; set; }
    }
}