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
        // === CONNECTION STRING (ESTABLISHES DATABASE CONNECTION) ===
        private readonly string _connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=CrimeVisionDB;Trusted_Connection=True;MultipleActiveResultSets=true";

        // === ORIGINAL CODE (DO NOT MODIFY) ===
        public IActionResult Dashboard()
        {
            var admin = HttpContext.Session.GetString("Admin");

            if (admin == null)
            {
                return RedirectToAction("AdminLogin", "Account");
            }

            return View();
        }
        // =============================================


        // === NEW USERS CODE (ADDED SEPARATELY BELOW) ===

        // 1. Action method to display the list of users
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
                string query = "SELECT Id, FullName, Email, CNIC, PhoneNumber, Status FROM Users";
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
                                FullName = dr["FullName"].ToString(),
                                Email = dr["Email"].ToString(),
                                CNIC = dr["CNIC"].ToString(),
                                PhoneNumber = dr["PhoneNumber"] != DBNull.Value ? dr["PhoneNumber"].ToString() : "",
                                Status = dr["Status"].ToString()
                            });
                        }
                    }
                }
            }

            return View(usersList);
        }

        // 2. Saves data to the database when the user submits the form
        [HttpPost]
        public IActionResult AddUser(User user)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = "INSERT INTO Users (FullName, Email, CNIC, PhoneNumber, Status) VALUES (@FullName, @Email, @CNIC, @PhoneNumber, 'Active')";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@FullName", user.FullName);
                    cmd.Parameters.AddWithValue("@Email", user.Email);
                    cmd.Parameters.AddWithValue("@CNIC", user.CNIC);
                    cmd.Parameters.AddWithValue("@PhoneNumber", user.PhoneNumber ?? (object)DBNull.Value);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }

            // Successfully saves and takes the admin back to the registry list
            return RedirectToAction("Users");
        }

        // 3. Opens the dynamic registration form view page for inputting citizen data
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

        // ==============================================================
        // 🚨 FIXED & CLEANED: CRIME REPORTS METHOD (SINGLE BLOCK)
        // ==============================================================
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
                            complaint.Add("CrimeType", reader["CrimeType"].ToString());
                            complaint.Add("IncidentDate", Convert.ToDateTime(reader["IncidentDate"]).ToString("dd/MM/yyyy"));
                            complaint.Add("Location", reader["Location"].ToString());
                            complaint.Add("Status", reader["Status"].ToString());
                            complaint.Add("Description", reader["Description"].ToString());

                            // Database ke naye columns ka data safely read ho raha hai
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