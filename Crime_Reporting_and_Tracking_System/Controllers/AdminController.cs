using Microsoft.AspNetCore.Mvc;

namespace Crime_Reporting_and_Tracking_System.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult Dashboard()
        {
            var admin =
                HttpContext.Session.GetString("Admin");

            if (admin == null)
            {
                return RedirectToAction("AdminLogin", "Account");
            }

            return View();
        }
    }
}