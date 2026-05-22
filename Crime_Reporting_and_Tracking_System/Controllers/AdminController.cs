using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Crime_Reporting_and_Tracking_System.Models;
using System.Collections.Generic;
using System;
using Microsoft.AspNetCore.Http;

namespace Crime_Reporting_and_Tracking_System.Controllers
{
    public class AdminController : Controller
    {
        // === CONNECTION STRING ===
        private readonly string _connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=CrimeVisionDB;Trusted_Connection=True;MultipleActiveResultSets=true";

        // === DASHBOARD ===
        public IActionResult Dashboard()
        {
            var admin = HttpContext.Session.GetString("Admin");
            if (admin == null)
            {
                return RedirectToAction("AdminLogin", "Account");
            }

            return View();
        }

        // === USERS SECTION ===

        // 1. Display list of users
        public IActionResult Users()
        {
            var admin = HttpContext.Session.GetString("Admin");
            if (admin == null)
            {
                return RedirectToAction("AdminLogin", "Account");
            }

            List<User> usersList = new List<User>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                // 🛠️ Error 1 Fixed: [Users] table name ko brackets mein kiya taake SQL syntax error na aaye
                string query = "SELECT Id, FullName, Email, CNIC, PhoneNumber, Status FROM [Users]";
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
                                Status = dr["Status"] != DBNull.Value ? dr["Status"].ToString() : ""
                            });
                        }
                    }
                }
            }

            return View(usersList);
        }

        // 2. Open registration form
        [HttpGet]
        public IActionResult CreateUser()
        {
            var admin = HttpContext.Session.GetString("Admin");
            if (admin == null)
            {
                return RedirectToAction("AdminLogin", "Account");
            }

            return View();
        }

        // 3. Save submitted user data
        [HttpPost]
        public IActionResult AddUser(User user)
        {
            var admin = HttpContext.Session.GetString("Admin");
            if (admin == null)
            {
                return RedirectToAction("AdminLogin", "Account");
            }

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                // 🛠️ Error 1 Fixed: Table name secured [Users]
                string query = "INSERT INTO [Users] (FullName, Email, CNIC, PhoneNumber, Status, Password) VALUES (@FullName, @Email, @CNIC, @PhoneNumber, 'Active', @Password)";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@FullName", user.FullName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Email", user.Email ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@CNIC", user.CNIC ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PhoneNumber", user.PhoneNumber ?? (object)DBNull.Value);

                    // 🛠️ Error 2 Fixed: Password binding double-check safety block
                    string securePassword = "Default@123";
                    if (user != null && !string.IsNullOrEmpty(user.Password))
                    {
                        securePassword = user.Password;
                    }
                    cmd.Parameters.AddWithValue("@Password", securePassword);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }

            return RedirectToAction("Users");
        }


        // === CRIME REPORTS SECTION ===

        [HttpGet]
        public IActionResult CrimeReports()
        {
            var admin = HttpContext.Session.GetString("Admin");
            if (admin == null)
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

                            // 🛠️ Error 3 Fixed: Har string column ko DBNull check ke sath safely read kiya hai
                            complaint.Add("CrimeType", reader["CrimeType"] != DBNull.Value ? reader["CrimeType"].ToString() : "N/A");

                            // Safe Date Conversion
                            string formattedDate = reader["IncidentDate"] != DBNull.Value
                                ? Convert.ToDateTime(reader["IncidentDate"]).ToString("dd/MM/yyyy")
                                : "N/A";
                            complaint.Add("IncidentDate", formattedDate);

                            complaint.Add("Location", reader["Location"] != DBNull.Value ? reader["Location"].ToString() : "N/A");
                            complaint.Add("Status", reader["Status"] != DBNull.Value ? reader["Status"].ToString() : "Pending");
                            complaint.Add("Description", reader["Description"] != DBNull.Value ? reader["Description"].ToString() : "");

                            // Safe reading for newer columns
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
        public IActionResult ApproveComplaint(int id)
        {
            var admin = HttpContext.Session.GetString("Admin");
            if (admin == null)
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