using System.ComponentModel.DataAnnotations;

namespace Crime_Reporting_and_Tracking_System.Models
{
    public class AdminLogin
    {
        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; }
    }
}