using Crime_Reporting_and_Tracking_System.Data;
using Crime_Reporting_and_Tracking_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Crime_Reporting_and_Tracking_System.Controllers
{
    public class AdminController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly ApplicationDbContext _context;

        public AdminController(IConfiguration configuration, ApplicationDbContext context)
        {
            _configuration = configuration;
            _context = context;
            _connectionString = _configuration.GetConnectionString("CrimeDB")
                                ?? "Server=(localdb)\\MSSQLLocalDB;Database=CrimeVisionDB;Trusted_Connection=True;MultipleActiveResultSets=true";
        }

        // Helper Method: DRY (Don't Repeat Yourself) principle ke liye session check function
        private bool IsAdminAuthenticated()
        {
            return HttpContext.Session.GetString("Admin") != null;
        }

        // ==========================================
        // 1. DASHBOARD OVERVIEW
        // ==========================================
        public IActionResult Dashboard()
        {
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");
            return View();
        }

        // ==========================================
        // 2. CITIZENS REGISTRY LISTING
        // ==========================================
        [HttpGet]
        public IActionResult Users()
        {
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");

            List<User> usersList = new List<User>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = "SELECT Id, FullName, Email, CNIC, PhoneNumber, ISNULL(ProfileImage, 'uploads/default-avatar.png') AS ProfileImage, ISNULL(Status, 'Active') AS Status FROM [Users] ORDER BY Id DESC";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            usersList.Add(new User
                            {
                                Id = Convert.ToInt32(dr["Id"]),
                                FullName = dr["FullName"] != DBNull.Value ? dr["FullName"].ToString() : "",
                                Email = dr["Email"] != DBNull.Value ? dr["Email"].ToString() : "",
                                CNIC = dr["CNIC"] != DBNull.Value ? dr["CNIC"].ToString() : "",
                                PhoneNumber = dr["PhoneNumber"] != DBNull.Value ? dr["PhoneNumber"].ToString() : "",
                                ProfileImage = dr["ProfileImage"].ToString(),
                                Status = dr["Status"].ToString()
                            });
                        }
                    }
                }
            }
            return View(usersList);
        }

        // ==========================================
        // 3. CREATE CITIZEN PROFILE (GET & POST)
        // ==========================================
        [HttpGet]
        public IActionResult CreateUser()
        {
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateUser(User user, IFormFile ProfilePicture)
        {
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");

            string imagePath = "uploads/default-avatar.png";

            if (ProfilePicture != null && ProfilePicture.Length > 0)
            {
                string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(ProfilePicture.FileName);
                string uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

                if (!Directory.Exists(uploadFolder))
                    Directory.CreateDirectory(uploadFolder);

                string fullPath = Path.Combine(uploadFolder, fileName);
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    ProfilePicture.CopyTo(stream);
                }
                imagePath = "uploads/" + fileName;
            }

            // Check if user already exists
            bool identityExists = false;
            string checkQuery = "SELECT COUNT(1) FROM [Users] WHERE CNIC = @CNIC OR Email = @Email";

            using (SqlConnection checkConn = new SqlConnection(_connectionString))
            {
                using (SqlCommand checkCmd = new SqlCommand(checkQuery, checkConn))
                {
                    checkCmd.Parameters.AddWithValue("@CNIC", user.CNIC ?? (object)DBNull.Value);
                    checkCmd.Parameters.AddWithValue("@Email", user.Email ?? (object)DBNull.Value);

                    checkConn.Open();
                    int exists = Convert.ToInt32(checkCmd.ExecuteScalar());
                    if (exists > 0) identityExists = true;
                }
            }

            if (identityExists)
            {
                ModelState.AddModelError("", "Security Alert: A profile matching this CNIC or Email already exists.");
                return View(user);
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    string query = "INSERT INTO [Users] (FullName, Email, CNIC, PhoneNumber, Status, Password, ProfileImage) " +
                                   "VALUES (@FullName, @Email, @CNIC, @PhoneNumber, 'Active', @Password, @ProfileImage)";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@FullName", user.FullName ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Email", user.Email ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@CNIC", user.CNIC ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@PhoneNumber", user.PhoneNumber ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ProfileImage", imagePath);

                        string securePassword = string.IsNullOrEmpty(user.Password) ? "Default@123" : user.Password;
                        cmd.Parameters.AddWithValue("@Password", securePassword);

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601)
                {
                    ModelState.AddModelError("", "Database Conflict: This record violates security constraint policies.");
                }
                else
                {
                    ModelState.AddModelError("", "Data Server Link Error: " + ex.Message);
                }
                return View(user);
            }

            return RedirectToAction("Users");
        }

        // ==========================================
        // 4. EDIT CITIZEN DASHBOARD (GET & POST)
        // ==========================================
        [HttpGet]
        public IActionResult EditUser(int id)
        {
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");

            User user = new User();

            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                string query = "SELECT Id, FullName, Email, CNIC, PhoneNumber, ISNULL(ProfileImage, 'uploads/default-avatar.png') AS ProfileImage, ISNULL(Status, 'Active') AS Status FROM [Users] WHERE Id = @id";
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    con.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            user.Id = Convert.ToInt32(reader["Id"]);
                            user.FullName = reader["FullName"].ToString();
                            user.Email = reader["Email"].ToString();
                            user.CNIC = reader["CNIC"].ToString();
                            user.PhoneNumber = reader["PhoneNumber"].ToString();
                            user.ProfileImage = reader["ProfileImage"].ToString();
                            user.Status = reader["Status"].ToString();
                        }
                    }
                }
            }
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditUser(User user, IFormFile NewProfilePicture)
        {
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");

            string imagePath = string.IsNullOrEmpty(user.ProfileImage) ? "uploads/default-avatar.png" : user.ProfileImage;

            if (NewProfilePicture != null && NewProfilePicture.Length > 0)
            {
                string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(NewProfilePicture.FileName);
                string uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

                string fullPath = Path.Combine(uploadFolder, fileName);
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    NewProfilePicture.CopyTo(stream);
                }
                imagePath = "uploads/" + fileName;
            }

            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                string query = "UPDATE [Users] SET FullName = @name, Email = @email, PhoneNumber = @phone, ProfileImage = @img, Status = @status WHERE Id = @id";
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@name", user.FullName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@email", user.Email ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@phone", user.PhoneNumber ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@img", imagePath);
                    cmd.Parameters.AddWithValue("@status", user.Status ?? "Active");
                    cmd.Parameters.AddWithValue("@id", user.Id);

                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            return RedirectToAction("Users");
        }

        // ==========================================
        // 5. BIOMETRIC PROFILE DETAILS VIEW
        // ==========================================
        [HttpGet]
        public IActionResult UserDetails(int id)
        {
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");

            User user = new User();

            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                string query = "SELECT Id, FullName, Email, CNIC, PhoneNumber, ISNULL(ProfileImage, 'uploads/default-avatar.png') AS ProfileImage, ISNULL(Status, 'Active') AS Status FROM [Users] WHERE Id = @id";
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    con.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            user.Id = Convert.ToInt32(reader["Id"]);
                            user.FullName = reader["FullName"].ToString();
                            user.Email = reader["Email"].ToString();
                            user.CNIC = reader["CNIC"].ToString();
                            user.PhoneNumber = reader["PhoneNumber"].ToString();
                            user.ProfileImage = reader["ProfileImage"].ToString();
                            user.Status = reader["Status"].ToString();
                        }
                    }
                }
            }
            return View(user);
        }

        // ==========================================
        // 6. REVOKE / DELETE CITIZEN ACCESS
        // ==========================================
        [HttpGet]
        public IActionResult DeleteUser(int id)
        {
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");

            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                string query = "DELETE FROM [Users] WHERE Id = @id";
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            return RedirectToAction("Users");
        }

        // ==========================================
        // 7. CRIME COMPLAINTS INDEX
        // ==========================================
        [HttpGet]
        public IActionResult CrimeReports()
        {
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");

            List<dynamic> allComplaints = new List<dynamic>();

            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                string query = @"
            SELECT c.ID, c.CrimeType, c.IncidentDate, c.Location, c.Status, c.Description, 
                   c.CitizenName, c.CitizenPhone, u.ProfileImage,
                   STRING_AGG(o.Name + ' (' + o.Rank + ')', ', ') AS AssignedOfficers
            FROM Complaints c
            LEFT JOIN Users u ON c.CitizenPhone = u.PhoneNumber
            LEFT JOIN ComplaintAssignments ca ON c.ID = ca.ComplaintId
            LEFT JOIN Officers o ON ca.OfficerId = o.Id
            GROUP BY c.ID, c.CrimeType, c.IncidentDate, c.Location, c.Status, c.Description, c.CitizenName, c.CitizenPhone, u.ProfileImage
            ORDER BY c.ID DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    con.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var complaint = new System.Dynamic.ExpandoObject() as IDictionary<string, object>;
                            complaint.Add("ID", reader["ID"]);
                            complaint.Add("CrimeType", reader["CrimeType"] != DBNull.Value ? reader["CrimeType"].ToString() : "N/A");

                            string formattedDate = reader["IncidentDate"] != DBNull.Value
                                ? Convert.ToDateTime(reader["IncidentDate"]).ToString("dd/MM/yyyy")
                                : "N/A";
                            complaint.Add("IncidentDate", formattedDate);

                            complaint.Add("Location", reader["Location"] != DBNull.Value ? reader["Location"].ToString() : "N/A");
                            complaint.Add("Status", reader["Status"] != DBNull.Value ? reader["Status"].ToString() : "Pending");
                            complaint.Add("Description", reader["Description"] != DBNull.Value ? reader["Description"].ToString() : "");
                            complaint.Add("FullName", reader["CitizenName"] != DBNull.Value ? reader["CitizenName"].ToString() : "Anonymous");
                            complaint.Add("PhoneNumber", reader["CitizenPhone"] != DBNull.Value ? reader["CitizenPhone"].ToString() : "N/A");

                            // --- IMAGE PATH CORRECTION LOGIC ---
                            string dbImagePath = reader["ProfileImage"] != DBNull.Value ? reader["ProfileImage"].ToString() : "";
                            string finalImagePath = "";

                            if (!string.IsNullOrEmpty(dbImagePath))
                            {
                                // Agar path pehle se sahi format (/ ya http) mein hai to wahi rehne dein
                                if (dbImagePath.StartsWith("/") || dbImagePath.StartsWith("~") || dbImagePath.StartsWith("http"))
                                {
                                    finalImagePath = dbImagePath;
                                }
                                else
                                {
                                    // AGAR DATABASE MEIN SIRF FILE NAME HAI (e.g. "my-pic.jpg"):
                                    // To aap apne wwwroot ke folder ka naam yahan likhein (Misaal ke tor par '/uploads/')
                                    finalImagePath = "/uploads/" + dbImagePath;
                                }
                            }

                            complaint.Add("CitizenImage", finalImagePath);
                            // ------------------------------------

                            complaint.Add("AssignedOfficers", reader["AssignedOfficers"] != DBNull.Value ? reader["AssignedOfficers"].ToString() : "");

                            allComplaints.Add(complaint);
                        }
                    }
                }
            }

            // Dropdown ke liye Officers ki list
            List<dynamic> officersList = new List<dynamic>();
            using (SqlConnection con2 = new SqlConnection(_connectionString))
            {
                string officerQuery = "SELECT Id, Name, Rank FROM Officers WHERE Status = 'Active' ORDER BY Name ASC";
                using (SqlCommand cmd2 = new SqlCommand(officerQuery, con2))
                {
                    con2.Open();
                    using (SqlDataReader reader2 = cmd2.ExecuteReader())
                    {
                        while (reader2.Read())
                        {
                            officersList.Add(new
                            {
                                Id = Convert.ToInt32(reader2["Id"]),
                                FullName = reader2["Name"].ToString() + " (" + reader2["Rank"].ToString() + ")"
                            });
                        }
                    }
                }
            }

            ViewBag.OfficersList = officersList;
            ViewBag.AllComplaints = allComplaints;

            return View("CrimeReports");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CompleteComplaint(int id)
        {
            // 1. Check karein ke admin login hai ya nahi
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");

            // 2. Database mein status update karne ki logic
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                // Case ko close ya complete karne ke liye status badal rahe hain
                string query = "UPDATE Complaints SET Status = 'Completed' WHERE ID = @id";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@id", id);

                    con.Open();
                    cmd.ExecuteNonQuery(); // Query run ho jayegi
                }
            }

            // 3. Status badalne ke baad wapas usi list wale page par bhej dein jahan updated data dikhega
            return RedirectToAction("CrimeReports");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AssignOfficer(int complaintId, int officerId)
        {
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");

            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                // Pehle check karein ke yeh officer is complaint ko pehle se assigned to nahi hai?
                string checkQuery = "SELECT COUNT(1) FROM ComplaintAssignments WHERE ComplaintId = @cId AND OfficerId = @oId";

                con.Open();
                using (SqlCommand checkCmd = new SqlCommand(checkQuery, con))
                {
                    checkCmd.Parameters.AddWithValue("@cId", complaintId);
                    checkCmd.Parameters.AddWithValue("@oId", officerId);
                    int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                    if (count == 0) // Agar assigned nahi hai to insert karein
                    {
                        string insertQuery = "INSERT INTO ComplaintAssignments (ComplaintId, OfficerId, AssignedDate) VALUES (@cId, @oId, @date)";
                        using (SqlCommand insertCmd = new SqlCommand(insertQuery, con))
                        {
                            insertCmd.Parameters.AddWithValue("@cId", complaintId);
                            insertCmd.Parameters.AddWithValue("@oId", officerId);
                            insertCmd.Parameters.AddWithValue("@date", DateTime.Now);
                            insertCmd.ExecuteNonQuery();
                        }

                        // Jaise hi koi officer assign ho, automatically status 'In Progress' kar dein
                        string updateStatusQuery = "UPDATE Complaints SET Status = 'In Progress' WHERE ID = @cId AND Status = 'Pending'";
                        using (SqlCommand updateCmd = new SqlCommand(updateStatusQuery, con))
                        {
                            updateCmd.Parameters.AddWithValue("@cId", complaintId);
                            updateCmd.ExecuteNonQuery();
                        }
                    }
                }
            }

            return RedirectToAction("CrimeReports");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ApproveComplaint(int id)
        {
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");

            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                string query = "UPDATE Complaints SET Status = 'In Progress' WHERE ID = @id";
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            return RedirectToAction("CrimeReports");
        }

        // ==========================================
        // 8. PUBLIC ALERTS MANAGEMENT
        // ==========================================
        [HttpGet]
        public IActionResult AlertsList()
        {
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");

            var alerts = _context.PublicAlerts.OrderByDescending(a => a.DateCreated).ToList();
            return View(alerts);
        }

        [HttpGet]
        public IActionResult CreateAlert()
        {
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAlert(PublicAlert model, IFormFile AlertAttachment)
        {
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");

            try
            {
                if (ModelState.IsValid)
                {
                    if (AlertAttachment != null && AlertAttachment.Length > 0)
                    {
                        string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + AlertAttachment.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            AlertAttachment.CopyTo(fileStream);
                        }

                        model.AttachmentPath = "uploads/" + uniqueFileName;
                    }
                    else
                    {
                        model.AttachmentPath = null;
                    }

                    model.DateCreated = DateTime.Now;

                    _context.PublicAlerts.Add(model);
                    _context.SaveChanges();

                    TempData["SuccessMessage"] = "Public security alert broadcasted successfully!";
                    return RedirectToAction("AlertsList");
                }

                ViewBag.ErrorMessage = "Validation failed. Please fill all required fields correctly.";
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "An error occurred while storing the alert: " + ex.Message;
            }

            return View(model);
        }

    }
}