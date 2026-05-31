using Crime_Reporting_and_Tracking_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;

namespace Crime_Reporting_and_Tracking_System.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration _configuration;

        public AccountController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // 🟢 User Login GET View
        public IActionResult UserLogin()
        {
            return View();
        }

        // 🟢 User Registration GET View
        public IActionResult UserRegister()
        {
            return View();
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

        // ==============================================================
        // 🔵 USER SUBMISSION METHODS (DATABASE BACKED)
        // ==============================================================

        // 🛠️ CHANGED: IFormFile ProfileImageFile parameter add kiya aur file saving logic implement ki
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UserRegister(string FullName, string Email, string CNIC, string PhoneNumber, string Password, IFormFile ProfileImageFile)
        {
            string conString = _configuration.GetConnectionString("CrimeDB");
            string imagePath = "uploads/default-avatar.png"; // Default fallback agar user image select na kare

            // 📸 Checking if user has uploaded a profile picture
            if (ProfileImageFile != null && ProfileImageFile.Length > 0)
            {
                // Unique filename using Guid to avoid overwriting
                string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(ProfileImageFile.FileName);

                // wwwroot/uploads directory target setup
                string uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

                // Creates directory if it doesn't exist
                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                string fullPath = Path.Combine(uploadFolder, fileName);

                // Saving image file stream to server disk
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    ProfileImageFile.CopyTo(stream);
                }

                // Short relative path for DB persistence
                imagePath = "uploads/" + fileName;
            }

            using (SqlConnection con = new SqlConnection(conString))
            {
                // ProfileImage column ko insert query mein add kiya gaya hai
                string query = "INSERT INTO Users (FullName, Email, CNIC, PhoneNumber, Password, ProfileImage) VALUES (@name, @email, @cnic, @phone, @pass, @img)";
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@name", FullName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@email", Email ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@cnic", CNIC ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@phone", PhoneNumber ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@pass", Password);
                    cmd.Parameters.AddWithValue("@img", imagePath); // Image path database mein save ho raha hai

                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            return RedirectToAction("UserLogin", "Account");
        }

        // When user submits the login form (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UserLogin(string Identifier, string Password)
        {
            string conString = _configuration.GetConnectionString("CrimeDB");

            using (SqlConnection con = new SqlConnection(conString))
            {
                // Login via Email or CNIC or Phone Number
                string query = "SELECT * FROM Users WHERE (Email=@id OR CNIC=@id OR PhoneNumber=@id) AND Password=@pass";
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@id", Identifier);
                    cmd.Parameters.AddWithValue("@pass", Password);

                    con.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // Storing active login data into active Session keys
                            HttpContext.Session.SetString("UserName", reader["FullName"].ToString());
                            HttpContext.Session.SetString("UserPhone", reader["PhoneNumber"].ToString());
                            HttpContext.Session.SetString("UserEmail", reader["Email"].ToString());

                            // 💡 ADDED: Login ke waqt hi image path session mein save kar liya takay dashboard/navbar par direct show ho sake
                            string dbImage = reader["ProfileImage"] != DBNull.Value ? reader["ProfileImage"].ToString() : "";
                            HttpContext.Session.SetString("UserProfileImage", string.IsNullOrEmpty(dbImage) ? "uploads/default-avatar.png" : dbImage);

                            return RedirectToAction("CitizenDashboard", "Account");
                        }
                        else
                        {
                            ViewBag.Error = "Invalid Login Credentials";
                            return View();
                        }
                    }
                }
            }
        }

        [HttpGet]
        public IActionResult CitizenDashboard()
        {
            List<dynamic> complaintsList = new List<dynamic>();
            int totalCases = 0;
            int activeCases = 0;
            int resolvedCases = 0;

            string conString = _configuration.GetConnectionString("CrimeDB");

            using (SqlConnection con = new SqlConnection(conString))
            {
                string query = "SELECT ID, CrimeType, IncidentDate, Location, Status FROM Complaints ORDER BY ID DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    con.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        (complaintsList).Clear();
                        while (reader.Read())
                        {
                            var complaint = new System.Dynamic.ExpandoObject() as IDictionary<string, object>;
                            complaint.Add("ID", reader["ID"]);
                            complaint.Add("CrimeType", reader["CrimeType"].ToString());
                            complaint.Add("IncidentDate", Convert.ToDateTime(reader["IncidentDate"]).ToString("dd/MM/yyyy"));
                            complaint.Add("Location", reader["Location"].ToString());
                            complaint.Add("Status", reader["Status"].ToString());

                            complaintsList.Add(complaint);

                            totalCases++;
                            string status = reader["Status"].ToString().ToLower();
                            if (status.Contains("progress") || status.Contains("pending"))
                            {
                                activeCases++;
                            }
                            else if (status.Contains("resolved"))
                            {
                                resolvedCases++;
                            }
                        }
                    }
                }
            }

            ViewBag.TotalCases = totalCases;
            ViewBag.ActiveCases = activeCases;
            ViewBag.ResolvedCases = resolvedCases;
            ViewBag.Complaints = complaintsList;

            return View("~/Views/Citizen/Dashboard.cshtml");
        }

        // 🟢 FIXED: Fetching active records so MyComplaints view loop works seamlessly!
        [HttpGet]
        public IActionResult MyComplaints()
        {
            List<dynamic> complaintsList = new List<dynamic>();
            string conString = _configuration.GetConnectionString("CrimeDB");

            using (SqlConnection con = new SqlConnection(conString))
            {
                string query = "SELECT ID, CrimeType, Location, Status FROM Complaints ORDER BY ID DESC";
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    con.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var complaint = new System.Dynamic.ExpandoObject() as IDictionary<string, object>;
                            complaint.Add("ID", reader["ID"]);
                            complaint.Add("CrimeType", reader["CrimeType"].ToString());
                            complaint.Add("Location", reader["Location"].ToString());
                            complaint.Add("Status", reader["Status"].ToString());

                            complaintsList.Add(complaint);
                        }
                    }
                }
            }

            ViewBag.Complaints = complaintsList;
            return View("~/Views/Citizen/MyComplaints.cshtml");
        }

        // ==============================================================
        // 🚨 SUBMIT COMPLAINT METHOD
        // ==============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SubmitComplaint(string CrimeType, DateTime IncidentDate, string Location, string Description)
        {
            string conString = _configuration.GetConnectionString("CrimeDB");

            // Session values are fetched or cleanly defaulted
            string citizenName = HttpContext.Session.GetString("UserName") ?? "Nafeesa Haroon";
            string citizenPhone = HttpContext.Session.GetString("UserPhone") ?? "03001234567";

            using (SqlConnection con = new SqlConnection(conString))
            {
                string query = "INSERT INTO Complaints (CrimeType, IncidentDate, Location, Description, Status, CitizenName, CitizenPhone) " +
                               "VALUES (@ct, @id, @loc, @desc, 'Pending Approval', @name, @phone)";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@ct", CrimeType ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", IncidentDate);
                    cmd.Parameters.AddWithValue("@loc", Location ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@desc", Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@name", citizenName);
                    cmd.Parameters.AddWithValue("@phone", citizenPhone);

                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }

            return RedirectToAction("MyComplaints");
        }

        // ==============================================================
        // 🚨 PUBLIC ALERTS METHODS
        // ==============================================================
        [HttpGet]
        public IActionResult PublicAlerts()
        {
            List<dynamic> alertsList = new List<dynamic>();
            string conString = _configuration.GetConnectionString("CrimeDB");

            using (SqlConnection con = new SqlConnection(conString))
            {
                string query = "SELECT ID, Title, Description, AlertLevel, Location, DateCreated FROM PublicAlerts ORDER BY ID DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    con.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var alert = new System.Dynamic.ExpandoObject() as IDictionary<string, object>;
                            alert.Add("ID", reader["ID"]);
                            alert.Add("Title", reader["Title"].ToString());
                            alert.Add("Description", reader["Description"].ToString());
                            alert.Add("AlertLevel", reader["AlertLevel"].ToString());
                            alert.Add("Location", reader["Location"].ToString());
                            alert.Add("DateCreated", Convert.ToDateTime(reader["DateCreated"]).ToString("dd/MM/yyyy hh:mm tt"));

                            alertsList.Add(alert);
                        }
                    }
                }
            }

            ViewBag.Alerts = alertsList;
            return View("~/Views/Citizen/PublicAlerts.cshtml");
        }

        [HttpGet]
        public IActionResult CreateAlert()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAlert(string title, string description, string alertLevel, string location)
        {
            string conString = _configuration.GetConnectionString("CrimeDB");

            using (SqlConnection con = new SqlConnection(conString))
            {
                string query = "INSERT INTO PublicAlerts (Title, Description, AlertLevel, Location, DateCreated) " +
                               "VALUES (@Title, @Description, @AlertLevel, @Location, GETDATE())";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Title", title ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Description", description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@AlertLevel", alertLevel ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Location", location ?? (object)DBNull.Value);

                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }

            return RedirectToAction("PublicAlerts", "Account");
        }

        [HttpGet]
        public IActionResult AlertDetails(int id)
        {
            string conString = _configuration.GetConnectionString("CrimeDB");
            dynamic targetAlert = new System.Dynamic.ExpandoObject();
            var alertDict = (IDictionary<string, object>)targetAlert;

            using (SqlConnection con = new SqlConnection(conString))
            {
                string query = "SELECT ID, Title, Description, AlertLevel, Location, DateCreated FROM PublicAlerts WHERE ID = @Id";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    con.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            alertDict.Add("ID", reader["ID"]);
                            alertDict.Add("Title", reader["Title"].ToString());
                            alertDict.Add("Description", reader["Description"].ToString());
                            alertDict.Add("AlertLevel", reader["AlertLevel"].ToString());
                            alertDict.Add("Location", reader["Location"].ToString());
                            alertDict.Add("DateCreated", Convert.ToDateTime(reader["DateCreated"]).ToString("dd/MM/yyyy hh:mm tt"));
                        }
                        else
                        {
                            return RedirectToAction("PublicAlerts");
                        }
                    }
                }
            }

            ViewBag.SelectedAlert = targetAlert;
            return View("~/Views/Citizen/AlertDetails.cshtml");
        }

        // ==============================================================
        // ⚙️ PROFILE SETTINGS METHODS (UPDATED TO STRONGLY TYPED MODEL)
        // ==============================================================

        [HttpGet]
        public IActionResult ProfileSettings()
        {
            string userEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(userEmail)) return RedirectToAction("UserLogin");

            string conString = _configuration.GetConnectionString("CrimeDB");
            User user = new User();

            using (SqlConnection con = new SqlConnection(conString))
            {
                string query = "SELECT FullName, Email, CNIC, PhoneNumber, ProfileImage FROM Users WHERE Email = @email";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@email", userEmail);

                con.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        user.FullName = reader["FullName"].ToString();
                        user.Email = reader["Email"].ToString();
                        user.CNIC = reader["CNIC"].ToString();
                        user.PhoneNumber = reader["PhoneNumber"] != DBNull.Value ? reader["PhoneNumber"].ToString() : "";

                        string dbImage = reader["ProfileImage"] != DBNull.Value ? reader["ProfileImage"].ToString() : "";
                        user.ProfileImage = string.IsNullOrEmpty(dbImage) ? "uploads/default-avatar.png" : dbImage;
                    }
                }
            }

            return View("~/Views/Citizen/ProfileSettings.cshtml", user);
        }

        [HttpPost]
        public IActionResult UpdateProfile(string FullName, string Email, string PhoneNumber, IFormFile ProfileImageFile)
        {
            string userEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(userEmail)) return RedirectToAction("UserLogin");

            string conString = _configuration.GetConnectionString("CrimeDB");
            string imagePath = null;

            if (ProfileImageFile != null && ProfileImageFile.Length > 0)
            {
                string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(ProfileImageFile.FileName);
                string uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                string fullPath = Path.Combine(uploadFolder, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    ProfileImageFile.CopyTo(stream);
                }

                imagePath = "uploads/" + fileName;
            }

            using (SqlConnection con = new SqlConnection(conString))
            {
                string query = "UPDATE Users SET FullName = @name, Email = @newEmail, PhoneNumber = @phone";
                if (imagePath != null)
                {
                    query += ", ProfileImage = @img";
                }
                query += " WHERE Email = @oldEmail";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@name", FullName);
                cmd.Parameters.AddWithValue("@newEmail", Email);
                cmd.Parameters.AddWithValue("@phone", (object)PhoneNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@oldEmail", userEmail);
                if (imagePath != null)
                {
                    cmd.Parameters.AddWithValue("@img", imagePath);
                }

                con.Open();
                int rows = cmd.ExecuteNonQuery();
                if (rows > 0)
                {
                    HttpContext.Session.SetString("UserEmail", Email);
                    HttpContext.Session.SetString("UserName", FullName);
                    if (imagePath != null)
                    {
                        HttpContext.Session.SetString("UserProfileImage", imagePath);
                    }
                    TempData["SuccessMessage"] = "Profile details updated successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update profile.";
                }
            }

            return RedirectToAction("ProfileSettings");
        }

        [HttpPost]
        public IActionResult ChangePassword(string CurrentPassword, string NewPassword, string ConfirmPassword)
        {
            string userEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(userEmail)) return RedirectToAction("UserLogin");

            if (NewPassword != ConfirmPassword)
            {
                TempData["ErrorMessage"] = "New password and confirm password do not match.";
                return RedirectToAction("ProfileSettings");
            }

            string conString = _configuration.GetConnectionString("CrimeDB");

            using (SqlConnection con = new SqlConnection(conString))
            {
                con.Open();

                string checkQuery = "SELECT Password FROM Users WHERE Email = @email";
                SqlCommand checkCmd = new SqlCommand(checkQuery, con);
                checkCmd.Parameters.AddWithValue("@email", userEmail);
                string dbPassword = checkCmd.ExecuteScalar()?.ToString();

                if (dbPassword != CurrentPassword)
                {
                    TempData["ErrorMessage"] = "Current password is incorrect.";
                    return RedirectToAction("ProfileSettings");
                }

                string updateQuery = "UPDATE Users SET Password = @newPass WHERE Email = @email";
                SqlCommand updateCmd = new SqlCommand(updateQuery, con);
                updateCmd.Parameters.AddWithValue("@newPass", NewPassword);
                updateCmd.Parameters.AddWithValue("@email", userEmail);
                updateCmd.ExecuteNonQuery();
                TempData["SuccessMessage"] = "Password changed successfully!";
            }

            return RedirectToAction("ProfileSettings");
        }

        // ==============================================================
        // 🚀 NEWLY ADDED: LOGOUT AND DELETE ACCOUNT METHODS
        // ==============================================================

        // 🟢 User / Admin Logout Action
        [HttpGet]
        public IActionResult LogOut()
        {
            HttpContext.Session.Clear(); // Clears all structural storage tokens instantly
            return RedirectToAction("UserLogin", "Account");
        }

        // 🔴 Permanent Account Erasure Process
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAccount()
        {
            string userEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(userEmail)) return RedirectToAction("UserLogin");

            string conString = _configuration.GetConnectionString("CrimeDB");

            using (SqlConnection con = new SqlConnection(conString))
            {
                string query = "DELETE FROM Users WHERE Email = @email";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@email", userEmail);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }

            HttpContext.Session.Clear(); // Erasing configuration state post execution
            TempData["SuccessMessage"] = "Your account has been permanently deleted.";
            return RedirectToAction("UserLogin", "Account");
        }
    }
}