using System.ComponentModel.DataAnnotations;

namespace Crime_Reporting_and_Tracking_System.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string CNIC { get; set; }

        public string PhoneNumber { get; set; }

        public string Status { get; set; } = "Active";

        
        public string Password { get; set; }

        public string ProfileImage { get; set; }
    }
}