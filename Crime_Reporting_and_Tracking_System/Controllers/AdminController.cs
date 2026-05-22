using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using Crime_Reporting_and_Tracking_System.Models;
using System.Collections.Generic;
using System;
using System.IO;

namespace Crime_Reporting_and_Tracking_System.Controllers
{
    public class AdminController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public AdminController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("CrimeDB")
                                ?? "Server=(localdb)\\MSSQLLocalDB;Database=CrimeVisionDB;Trusted_Connection=True;MultipleActiveResultSets=true";
        }

        // ==========================================
        // 1. DASHBOARD OVERVIEW
        // ==========================================
        public IActionResult Dashboard()
        {
            if (HttpContext.Session.GetString("Admin") == null)
            {
                return RedirectToAction("AdminLogin", "Account");
            }
            return View();
        }

        // ==========================================
        // 2. CITIZENS REGISTRY LISTING (Main Page)
        // ==========================================
        [HttpGet]
        public IActionResult Users()
        {
            if (HttpContext.Session.GetString("Admin") == null)
            {
                return RedirectToAction("AdminLogin", "Account");
            }

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
            if (HttpContext.Session.GetString("Admin") == null)
            {
                return RedirectToAction("AdminLogin", "Account");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddUser(User user, IFormFile ProfilePicture)
        {
            if (HttpContext.Session.GetString("Admin") == null)
            {
                return RedirectToAction("AdminLogin", "Account");
            }

            // Fallback default avatar profile management
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

            // FIX 1: Alag se unified single block use karein pre-existence verify karne ke liye
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
                    if (exists > 0)
                    {
                        identityExists = true;
                    }
                }
            }

            // FIX 2: Agar duplicate data hai toh error messaging pass karke form reload karein
            if (identityExists)
            {
                ModelState.AddModelError("", "Security Alert: A profile matching this CNIC or Email structure already exists.");
                return View("CreateUser", user);
            }

            // FIX 3: Safe data push strategy execute karein
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
                return View("CreateUser", user);
            }

            return RedirectToAction("Users");
        }

        // ==========================================
        // 4. EDIT CITIZEN DASHBOARD (GET & POST)
        // ==========================================
        [HttpGet]
        public IActionResult EditUser(int id)
        {
            if (HttpContext.Session.GetString("Admin") == null)
            {
                return RedirectToAction("AdminLogin", "Account");
            }

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
            if (HttpContext.Session.GetString("Admin") == null)
            {
                return RedirectToAction("AdminLogin", "Account");
            }

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
            if (HttpContext.Session.GetString("Admin") == null)
            {
                return RedirectToAction("AdminLogin", "Account");
            }

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
            if (HttpContext.Session.GetString("Admin") == null)
            {
                return RedirectToAction("AdminLogin", "Account");
            }

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
            if (HttpContext.Session.GetString("Admin") == null)
            {
                return RedirectToAction("AdminLogin", "Account");
            }

            List<dynamic> allComplaints = new List<dynamic>();

            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                string query = "SELECT ID, CrimeType, IncidentDate, Location, Status, Description, CitizenName, CitizenPhone FROM Complaints ORDER BY ID DESC";
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

                            allComplaints.Add(complaint);
                        }
                    }
                }
            }

            ViewBag.AllComplaints = allComplaints;
            return View("CrimeReports");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ApproveComplaint(int id)
        {
            if (HttpContext.Session.GetString("Admin") == null)
            {
                return RedirectToAction("AdminLogin", "Account");
            }

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
    }
}