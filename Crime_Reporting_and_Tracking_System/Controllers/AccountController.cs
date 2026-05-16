using Crime_Reporting_and_Tracking_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Crime_Reporting_and_Tracking_System.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration _configuration;

        public AccountController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult AdminLogin()
        {
            return View();
        }

        [HttpPost]
        public IActionResult AdminLogin(AdminLogin a)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "All fields are required";
                return View(a);
            }

            int attempts = HttpContext.Session.GetInt32("Attempts") ?? 0;
            string lockTimeStr = HttpContext.Session.GetString("LockTime");

            // 🔴 LOCK CHECK WITH TIMER
            if (!string.IsNullOrEmpty(lockTimeStr))
            {
                DateTime lockTime = DateTime.Parse(lockTimeStr);

                if (DateTime.Now < lockTime)
                {
                    int secondsLeft = (int)(lockTime - DateTime.Now).TotalSeconds;

                    ViewBag.Error = $"Account locked. Try again in {secondsLeft} seconds.";

                    ViewBag.Locked = true;
                    ViewBag.SecondsLeft = secondsLeft;

                    return View(a);
                }
                else
                {
                    HttpContext.Session.Remove("LockTime");
                    HttpContext.Session.SetInt32("Attempts", 0);
                    attempts = 0;
                }
            }

            string conString = _configuration.GetConnectionString("CrimeDB");

            using (SqlConnection con = new SqlConnection(conString))
            {
                string query = "SELECT * FROM Admins WHERE Username=@u AND Password=@p";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@u", a.Username);
                cmd.Parameters.AddWithValue("@p", a.Password);

                con.Open();

                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.HasRows)
                {
                    HttpContext.Session.SetString("Admin", a.Username);
                    HttpContext.Session.SetInt32("Attempts", 0);
                    HttpContext.Session.Remove("LockTime");

                    return RedirectToAction("Dashboard", "Admin");
                }
                else
                {
                    attempts++;
                    HttpContext.Session.SetInt32("Attempts", attempts);

                    if (attempts >= 5)
                    {
                        DateTime lockTime = DateTime.Now.AddSeconds(5);
                        HttpContext.Session.SetString("LockTime", lockTime.ToString());

                        ViewBag.Error = "Too many failed attempts. Account locked for 5 seconds.";
                        ViewBag.Locked = true;
                        ViewBag.SecondsLeft = 5;
                    }
                    else
                    {
                        ViewBag.Error = "Invalid Username or Password";
                        ViewBag.Attempts = $"Attempts left: {5 - attempts}";
                    }
                }
            }

            return View(a);
        }
    }
}