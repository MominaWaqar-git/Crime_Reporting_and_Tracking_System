using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CrimeReportingSystem.Models
{
    public class Officer
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Officer Name is required.")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid Email Address. Must contain @ and a valid domain.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "CNIC is required.")]
        [RegularExpression(@"^\d{13}$", ErrorMessage = "CNIC must be exactly 13 digits without dashes.")]
        public string CNIC { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "Phone number must be exactly 11 digits.")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Rank is required.")]
        public string Rank { get; set; } // e.g., ASI, SI, SHO, DSP

        [Required(ErrorMessage = "Status is required.")]
        public string Status { get; set; } // Active, Suspended, On Leave

        [Required(ErrorMessage = "Police Station Name is required.")]
        public string StationName { get; set; }

        [Required(ErrorMessage = "Address is required.")]
        [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters.")]
        public string Address { get; set; }

        // Is me photo ka filename save hoga database me
        public string ProfilePicturePath { get; set; }
    }
}