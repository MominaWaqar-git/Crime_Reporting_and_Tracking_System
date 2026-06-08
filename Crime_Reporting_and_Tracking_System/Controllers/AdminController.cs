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
        // =========================================

        public ActionResult Dashboard()
        {
            // 1. Secure it!
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");

            int totalReports = 0, pendingCases = 0, resolvedCases = 0, inProgressCases = 0, totalUsers = 0;
            List<string> crimeTypes = new List<string>();
            List<int> crimeTotals = new List<int>();

            // 2. Wrap in a 'using' statement for safety
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                con.Open();

                // OPTIMIZATION: Ek hi query mein saare filtered counts nikal liye taake database par load na pade
                string countsQuery = @"
            SELECT 
                COUNT(*) AS TotalReports,
                SUM(CASE WHEN Status = 'Pending Approval' THEN 1 ELSE 0 END) AS PendingCases,
                SUM(CASE WHEN Status IN ('Resolved', 'Completed') THEN 1 ELSE 0 END) AS ResolvedCases,
                SUM(CASE WHEN Status = 'In Progress' THEN 1 ELSE 0 END) AS InProgressCases
            FROM Complaints
            WHERE CrimeType NOT IN ('Officer Communication', 'Direct Chat Reference')"; // FILTER APPLIED HERE

                using (SqlCommand countsCmd = new SqlCommand(countsQuery, con))
                using (SqlDataReader reader = countsCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        totalReports = Convert.ToInt32(reader["TotalReports"]);
                        pendingCases = reader["PendingCases"] != DBNull.Value ? Convert.ToInt32(reader["PendingCases"]) : 0;
                        resolvedCases = reader["ResolvedCases"] != DBNull.Value ? Convert.ToInt32(reader["ResolvedCases"]) : 0;
                        inProgressCases = reader["InProgressCases"] != DBNull.Value ? Convert.ToInt32(reader["InProgressCases"]) : 0;
                    }
                }

                // Total Users Count (Yeh Users table se hai, isme filter ki zaroorat nahi)
                using (SqlCommand usersCmd = new SqlCommand("SELECT COUNT(*) FROM [Users]", con))
                {
                    totalUsers = Convert.ToInt32(usersCmd.ExecuteScalar());
                }

                // CHART DATA FILTER: Yahan se bhi communication pipelines ko block kar diya hai
                string chartQuery = @"
            SELECT ISNULL(CrimeType, 'General') AS CrimeType, COUNT(*) AS Total 
            FROM Complaints 
            WHERE CrimeType NOT IN ('Officer Communication', 'Direct Chat Reference') -- FILTER APPLIED HERE
            GROUP BY CrimeType";

                using (SqlCommand chartCmd = new SqlCommand(chartQuery, con))
                using (SqlDataReader dr = chartCmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        crimeTypes.Add(dr["CrimeType"].ToString());
                        crimeTotals.Add(Convert.ToInt32(dr["Total"]));
                    }
                }
            }

            // If no database data exists yet, provide explicit chart defaults to avoid Javascript crashes
            if (!crimeTypes.Any())
            {
                crimeTypes.Add("No Data Available");
                crimeTotals.Add(0);
            }

            // Pass data to ViewBag safely
            ViewBag.TotalReports = totalReports;
            ViewBag.PendingCases = pendingCases;
            ViewBag.ResolvedCases = resolvedCases;
            ViewBag.InProgressCases = inProgressCases;
            ViewBag.TotalUsers = totalUsers;
            ViewBag.CrimeTypes = crimeTypes;
            ViewBag.CrimeTotals = crimeTotals;

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
        // 7. CRIME COMPLAINTS INDEX (COMPLETED & FIXED)
        // ==========================================
        [HttpGet]
        public IActionResult CrimeReports()
        {
            if (!IsAdminAuthenticated())
                return RedirectToAction("AdminLogin", "Account");

            List<dynamic> allComplaints = new List<dynamic>();

            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                string query = @"
            SELECT 
                c.ID, c.CrimeType, c.IncidentDate, c.Location, c.Status, c.Description, c.CitizenName, c.CitizenPhone,
                u.ProfileImage,
                MAX(e.FilePath) AS EvidenceFile,
                STRING_AGG(o.Name + ' (' + o.Rank + ')', ', ') AS AssignedOfficers
            FROM Complaints c
            LEFT JOIN Users u ON c.CitizenPhone = u.PhoneNumber
            LEFT JOIN ComplaintAssignments ca ON c.ID = ca.ComplaintId
            LEFT JOIN Officers o ON ca.OfficerId = o.Id
            LEFT JOIN Evidence e ON c.ID = e.ComplaintId
            WHERE c.CrimeType NOT IN ('Officer Communication', 'Direct Chat Reference')
            GROUP BY 
                c.ID, c.CrimeType, c.IncidentDate, c.Location, c.Status, 
                c.Description, c.CitizenName, c.CitizenPhone, u.ProfileImage
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

                            // CITIZEN IMAGE PATH SANITIZATION
                            string dbImagePath = reader["ProfileImage"] != DBNull.Value ? reader["ProfileImage"].ToString().Trim() : "";
                            string finalImagePath = "/images/default-avatar.png";

                            if (!string.IsNullOrEmpty(dbImagePath))
                            {
                                dbImagePath = dbImagePath.Replace("~", "").Replace("\\", "/");
                                if (dbImagePath.StartsWith("/") || dbImagePath.StartsWith("http"))
                                    finalImagePath = dbImagePath;
                                else if (dbImagePath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
                                    finalImagePath = "/" + dbImagePath;
                                else
                                    finalImagePath = "/uploads/" + dbImagePath.TrimStart('/');
                            }
                            complaint.Add("CitizenImage", finalImagePath);

                            // EVIDENCE PATH SANITIZATION
                            string evidencePath = "";
                            if (reader["EvidenceFile"] != DBNull.Value)
                            {
                                evidencePath = reader["EvidenceFile"].ToString().Trim().Replace("~", "").Replace("\\", "/");
                                if (evidencePath.StartsWith("/") || evidencePath.StartsWith("http"))
                                {
                                    if (evidencePath.StartsWith("/uploads/uploads/"))
                                        evidencePath = evidencePath.Replace("/uploads/uploads/", "/uploads/");
                                }
                                else if (evidencePath.StartsWith("uploads/evidence/", StringComparison.OrdinalIgnoreCase) ||
                                         evidencePath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
                                {
                                    evidencePath = "/" + evidencePath;
                                }
                                else
                                {
                                    evidencePath = "/uploads/evidence/" + evidencePath.TrimStart('/');
                                }
                            }
                            complaint.Add("EvidenceFile", evidencePath);
                            complaint.Add("AssignedOfficers", reader["AssignedOfficers"] != DBNull.Value ? reader["AssignedOfficers"].ToString() : "");

                            allComplaints.Add(complaint);
                        }
                    }
                }
            }

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

        // ==========================================
        // 3. ASSIGN OFFICER (FIXED & PIPELINE ISOLATED)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AssignOfficer(int complaintId, int officerId)
        {
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");

            string crimeType = "N/A", location = "N/A", formattedDate = "N/A";
            string currentOfficerName = "Officer", currentOfficerPhone = "N/A";
            List<string> allAssignedOfficersList = new List<string>();
            int chatId = 0;

            try
            {
                using (SqlConnection con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    // 1. Duplicate Assignment Check
                    string checkQuery = "SELECT COUNT(1) FROM ComplaintAssignments WHERE ComplaintId = @cId AND OfficerId = @oId";
                    int count = 0;
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, con))
                    {
                        checkCmd.Parameters.AddWithValue("@cId", complaintId);
                        checkCmd.Parameters.AddWithValue("@oId", officerId);
                        count = Convert.ToInt32(checkCmd.ExecuteScalar());
                    }

                    if (count == 0)
                    {
                        // 2. Insert Assignment
                        string insertQuery = "INSERT INTO ComplaintAssignments (ComplaintId, OfficerId, AssignedDate) VALUES (@cId, @oId, GETDATE())";
                        using (SqlCommand insertCmd = new SqlCommand(insertQuery, con))
                        {
                            insertCmd.Parameters.AddWithValue("@cId", complaintId);
                            insertCmd.Parameters.AddWithValue("@oId", officerId);
                            insertCmd.ExecuteNonQuery();
                        }
                    }

                    // 3. Update Complaint Status
                    string updateStatusQuery = "UPDATE Complaints SET Status = 'In Progress' WHERE ID = @cId";
                    using (SqlCommand updateCmd = new SqlCommand(updateStatusQuery, con))
                    {
                        updateCmd.Parameters.AddWithValue("@cId", complaintId);
                        updateCmd.ExecuteNonQuery();
                    }

                    // 4. Fetch Complaint Details
                    string compQuery = "SELECT CrimeType, Location, IncidentDate FROM Complaints WHERE ID = @cId";
                    using (SqlCommand compCmd = new SqlCommand(compQuery, con))
                    {
                        compCmd.Parameters.AddWithValue("@cId", complaintId);
                        using (SqlDataReader r1 = compCmd.ExecuteReader())
                        {
                            if (r1.Read())
                            {
                                crimeType = r1["CrimeType"] != DBNull.Value ? r1["CrimeType"].ToString() : "N/A";
                                location = r1["Location"] != DBNull.Value ? r1["Location"].ToString() : "N/A";
                                if (r1["IncidentDate"] != DBNull.Value)
                                {
                                    formattedDate = Convert.ToDateTime(r1["IncidentDate"]).ToString("dd/MM/yyyy");
                                }
                            }
                        }
                    }

                    // 5. Fetch Officer Details
                    string offQuery = "SELECT Name, PhoneNumber FROM Officers WHERE Id = @oId";
                    using (SqlCommand offCmd = new SqlCommand(offQuery, con))
                    {
                        offCmd.Parameters.AddWithValue("@oId", officerId);
                        using (SqlDataReader r2 = offCmd.ExecuteReader())
                        {
                            if (r2.Read())
                            {
                                currentOfficerName = r2["Name"].ToString();
                                currentOfficerPhone = r2["PhoneNumber"].ToString();
                            }
                        }
                    }

                    // 6. Fetch Co-Officers
                    string allOfficersQuery = @"
                        SELECT o.Name, o.PhoneNumber 
                        FROM ComplaintAssignments ca
                        JOIN Officers o ON ca.OfficerId = o.Id
                        WHERE ca.ComplaintId = @cId";

                    using (SqlCommand allOffCmd = new SqlCommand(allOfficersQuery, con))
                    {
                        allOffCmd.Parameters.AddWithValue("@cId", complaintId);
                        using (SqlDataReader r3 = allOffCmd.ExecuteReader())
                        {
                            while (r3.Read())
                            {
                                allAssignedOfficersList.Add($"{r3["Name"]} ({r3["PhoneNumber"]})");
                            }
                        }
                    }

                    // 7. Get or Create Chat Room (FIXED: Removed IsDeleted constraint to prevent duplicates)
                    string getChatQuery = "SELECT TOP 1 ChatId FROM GroupChats WHERE ComplaintId = @cId ORDER BY ChatId DESC";
                    using (SqlCommand getChatCmd = new SqlCommand(getChatQuery, con))
                    {
                        getChatCmd.Parameters.AddWithValue("@cId", complaintId);
                        var res = getChatCmd.ExecuteScalar();
                        if (res != null) chatId = Convert.ToInt32(res);
                    }

                    if (chatId == 0)
                    {
                        string createChatQuery = "INSERT INTO GroupChats (ComplaintId, IsDeleted) OUTPUT INSERTED.ChatId VALUES (@cId, 0)";
                        using (SqlCommand createChatCmd = new SqlCommand(createChatQuery, con))
                        {
                            createChatCmd.Parameters.AddWithValue("@cId", complaintId);
                            chatId = Convert.ToInt32(createChatCmd.ExecuteScalar());
                        }
                    }

                    // 8. Generate Alerts Text
                    string citizenAlert = $"🟢 *YOUR CASE HAS BEEN APPROVED & ASSIGNED* 🟢\n\n" +
                                          $"📂 *Case Reference:* #{complaintId}\n" +
                                          $"⚠️ *Crime Name:* {crimeType}\n" +
                                          $"👮 *Assigned Investigator:* {currentOfficerName}\n" +
                                          $"📞 *Officer Contact:* {currentOfficerPhone}";

                    // 9. Safe Message Insert (With Fallback Strategy)
                    try
                    {
                        string queryWithTime = "INSERT INTO ChatMessages (ChatId, SenderType, SenderName, MessageText, Timestamp) VALUES (@chatId, 'System', 'CrimeVision Bot', @msg, GETDATE())";
                        using (SqlCommand cmd = new SqlCommand(queryWithTime, con))
                        {
                            cmd.Parameters.AddWithValue("@chatId", chatId);
                            cmd.Parameters.AddWithValue("@msg", citizenAlert);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch (SqlException)
                    {
                        string queryNoTime = "INSERT INTO ChatMessages (ChatId, SenderType, SenderName, MessageText) VALUES (@chatId, 'System', 'CrimeVision Bot', @msg)";
                        using (SqlCommand cmdFallback = new SqlCommand(queryNoTime, con))
                        {
                            cmdFallback.Parameters.AddWithValue("@chatId", chatId);
                            cmdFallback.Parameters.AddWithValue("@msg", citizenAlert);
                            cmdFallback.ExecuteNonQuery();
                        }
                    }
                }

                TempData["SuccessMessage"] = "Officer assigned successfully and message logged!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "CRITICAL ERROR: " + ex.Message;
                System.Diagnostics.Debug.WriteLine("CRIMEVISION ERROR: " + ex.ToString());
            }

            return RedirectToAction("CrimeReports");
        }

        // ==========================================
        // 4. APPROVE COMPLAINT (Initial Room Setup - FIXED)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ApproveComplaint(int id)
        {
            if (!IsAdminAuthenticated()) return RedirectToAction("AdminLogin", "Account");

            int chatId = 0;
            string crimeType = "N/A";
            string location = "N/A";
            string citizenName = "Citizen";
            DateTime incidentDate = DateTime.Now;

            try
            {
                using (SqlConnection con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    // 1. Update status
                    string updateQuery = "UPDATE Complaints SET Status='Approved' WHERE ID=@id";
                    using (SqlCommand cmd = new SqlCommand(updateQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }

                    // 2. Fetch complaint data
                    string fetchQuery = "SELECT CrimeType, Location, CitizenName, IncidentDate FROM Complaints WHERE ID=@id";
                    using (SqlCommand cmd = new SqlCommand(fetchQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                crimeType = reader["CrimeType"] != DBNull.Value ? reader["CrimeType"].ToString() : "N/A";
                                location = reader["Location"] != DBNull.Value ? reader["Location"].ToString() : "N/A";
                                citizenName = reader["CitizenName"] != DBNull.Value ? reader["CitizenName"].ToString() : "Citizen";

                                if (reader["IncidentDate"] != DBNull.Value)
                                    incidentDate = Convert.ToDateTime(reader["IncidentDate"]);
                            }
                        }
                    }

                    // 3. Get or Create Chat (FIXED: Standardized query to avoid duplicates)
                    string getChat = "SELECT TOP 1 ChatId FROM GroupChats WHERE ComplaintId=@id ORDER BY ChatId DESC";
                    using (SqlCommand cmd = new SqlCommand(getChat, con))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        var res = cmd.ExecuteScalar();
                        if (res != null) chatId = Convert.ToInt32(res);
                    }

                    if (chatId == 0)
                    {
                        string createChat = "INSERT INTO GroupChats (ComplaintId, IsDeleted) OUTPUT INSERTED.ChatId VALUES (@id, 0)";
                        using (SqlCommand cmd = new SqlCommand(createChat, con))
                        {
                            cmd.Parameters.AddWithValue("@id", id);
                            chatId = Convert.ToInt32(cmd.ExecuteScalar());
                        }
                    }

                    // 4. MESSAGE GENERATION
                    string message = $"🎉 *CASE APPROVED*\n\nDear {citizenName},\n\nYour complaint has been approved.\n\nCase ID: #{id}\nCrime Type: {crimeType}\nLocation: {location}\nDate: {incidentDate:dd MMM yyyy}\n\nOfficer will be assigned soon.";

                    // 5. Safe Insert to ChatMessages
                    try
                    {
                        string insertMsg = "INSERT INTO ChatMessages (ChatId, SenderType, SenderName, MessageText, Timestamp) VALUES (@chatId, 'System', 'CrimeVision Bot', @msg, GETDATE())";
                        using (SqlCommand cmd = new SqlCommand(insertMsg, con))
                        {
                            cmd.Parameters.AddWithValue("@chatId", chatId);
                            cmd.Parameters.AddWithValue("@msg", message);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch (SqlException)
                    {
                        string insertMsgNoTime = "INSERT INTO ChatMessages (ChatId, SenderType, SenderName, MessageText) VALUES (@chatId, 'System', 'CrimeVision Bot', @msg)";
                        using (SqlCommand cmd = new SqlCommand(insertMsgNoTime, con))
                        {
                            cmd.Parameters.AddWithValue("@chatId", chatId);
                            cmd.Parameters.AddWithValue("@msg", message);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                TempData["SuccessMessage"] = "Complaint approved successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "ERROR IN APPROVAL: " + ex.Message;
            }

            return RedirectToAction("CrimeReports");
        }
        // ==========================================
        // 5. COMPLETE COMPLAINT (With Final Closure Message)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CompleteComplaint(int complaintId)
        {
            if (!IsAdminAuthenticated())
                return RedirectToAction("AdminLogin", "Account");

            int chatId = 0;
            string crimeType = "N/A";
            string citizenName = "Citizen";

            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                con.Open();

                // 1. Update
                string update = "UPDATE Complaints SET Status='Completed' WHERE ID=@id";
                using (SqlCommand cmd = new SqlCommand(update, con))
                {
                    cmd.Parameters.AddWithValue("@id", complaintId);
                    cmd.ExecuteNonQuery();
                }

                // 2. Fetch
                string fetch = "SELECT CrimeType, CitizenName FROM Complaints WHERE ID=@id";
                using (SqlCommand cmd = new SqlCommand(fetch, con))
                {
                    cmd.Parameters.AddWithValue("@id", complaintId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            crimeType = r["CrimeType"] != DBNull.Value ? r["CrimeType"].ToString() : "N/A";
                            citizenName = r["CitizenName"] != DBNull.Value ? r["CitizenName"].ToString() : "Citizen";
                        }
                    }
                }

                // 3. Chat
                string getChat = "SELECT ChatId FROM GroupChats WHERE ComplaintId=@id";
                using (SqlCommand cmd = new SqlCommand(getChat, con))
                {
                    cmd.Parameters.AddWithValue("@id", complaintId);
                    var res = cmd.ExecuteScalar();
                    if (res != null) chatId = Convert.ToInt32(res);
                }

                if (chatId > 0)
                {
                    string msg = $"✅ *CASE CLOSED*\n\nDear {citizenName},\n\nCase #{complaintId} has been completed.\n\nCrime: {crimeType}\n\nThank you for using CrimeVision.";

                    string insert = "INSERT INTO ChatMessages (ChatId, SenderType, SenderName, MessageText) VALUES (@chatId, 'System', 'CrimeVision Bot', @msg)";
                    using (SqlCommand cmd = new SqlCommand(insert, con))
                    {
                        cmd.Parameters.AddWithValue("@chatId", chatId);
                        cmd.Parameters.AddWithValue("@msg", msg);
                        cmd.ExecuteNonQuery();
                    }
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