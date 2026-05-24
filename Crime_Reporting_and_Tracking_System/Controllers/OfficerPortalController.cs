using Crime_Reporting_and_Tracking_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Dynamic;

namespace Crime_Reporting_and_Tracking_System.Controllers
{
    public class OfficerPortalController : Controller
    {
        private readonly IConfiguration _configuration;

        public OfficerPortalController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // 🟢 GET: /OfficerPortal/Login
        public IActionResult Login()
        {
            // Agar pehle se CNIC ya Email session mein hai toh direct dashboard bhej do
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("OfficerCNIC")))
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        // 🚨 POST: /OfficerPortal/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string CNIC)
        {
            // Check if input is empty
            if (string.IsNullOrEmpty(CNIC))
            {
                ViewBag.Error = "Please enter your official CNIC Number.";
                return View();
            }

            // Input formatting clean-up (agar dashes ke sath ya baghair enter karein, tab bhi chalay)
            string cleanCNIC = CNIC.Replace("-", "").Trim();

            string conString = _configuration.GetConnectionString("CrimeDB");

            using (SqlConnection con = new SqlConnection(conString))
            {
                // 🔥 CNIC BASED LINKING RULE: Admin jo profile add karega, uske unique CNIC par verification hogi
                // REPLACE(CNIC, '-', '') use kiya hai taake agar database ya input mein dashes ka farq ho toh crash na ho
                string query = "SELECT * FROM Officers WHERE REPLACE(CNIC, '-', '') = @cnic";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@cnic", cleanCNIC);

                    con.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string status = reader["Status"]?.ToString() ?? "Active";

                            // Admin Deactivation Rule Check
                            if (status.ToLower() != "active")
                            {
                                ViewBag.Error = "Your account is deactivated by the Admin.";
                                return View();
                            }

                            // Session Variables Mapping via Database Columns
                            HttpContext.Session.SetString("OfficerId", reader["Id"].ToString());
                            HttpContext.Session.SetString("OfficerName", reader["Name"].ToString());
                            HttpContext.Session.SetString("OfficerCNIC", reader["CNIC"].ToString());
                            HttpContext.Session.SetString("OfficerEmail", reader["Email"]?.ToString() ?? "N/A");
                            HttpContext.Session.SetString("OfficerRank", reader["Rank"].ToString());
                            HttpContext.Session.SetString("StationScope", reader["StationName"].ToString());
                            HttpContext.Session.SetString("OfficerImage", reader["ProfilePicturePath"]?.ToString() ?? "default-avatar.png");

                            return RedirectToAction("Dashboard");
                        }
                        else
                        {
                            ViewBag.Error = "No registered Officer found with this CNIC. Please contact Admin.";
                            return View();
                        }
                    }
                }
            }
        }

        // 🖥️ GET: /OfficerPortal/Dashboard
        [HttpGet]
        public IActionResult Dashboard()
        {
            string officerCNIC = HttpContext.Session.GetString("OfficerCNIC");
            string stationScope = HttpContext.Session.GetString("StationScope");

            if (string.IsNullOrEmpty(officerCNIC) || string.IsNullOrEmpty(stationScope))
            {
                return RedirectToAction("Login");
            }

            List<dynamic> assignedComplaints = new List<dynamic>();
            int totalScopeCases = 0;
            int activeScopeCases = 0;
            int resolvedScopeCases = 0;

            string conString = _configuration.GetConnectionString("CrimeDB");

            using (SqlConnection con = new SqlConnection(conString))
            {
                // 🔥 JURISDICTION LINKING: Officer jis station ka hai, use sirf wahan ki complaints nazar aayengi
                string query = "SELECT ID, CrimeType, IncidentDate, Location, Status, CitizenName FROM Complaints WHERE Location = @scope ORDER BY ID DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@scope", stationScope);
                    con.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var complaint = new ExpandoObject() as IDictionary<string, object>;
                            complaint.Add("ID", reader["ID"]);
                            complaint.Add("CrimeType", reader["CrimeType"].ToString());
                            complaint.Add("IncidentDate", Convert.ToDateTime(reader["IncidentDate"]).ToString("dd/MM/yyyy"));
                            complaint.Add("Location", reader["Location"].ToString());
                            complaint.Add("Status", reader["Status"].ToString());
                            complaint.Add("CitizenName", reader["CitizenName"].ToString());

                            assignedComplaints.Add(complaint);

                            totalScopeCases++;
                            string status = reader["Status"].ToString().ToLower();
                            if (status.Contains("progress") || status.Contains("pending") || status.Contains("approval"))
                            {
                                activeScopeCases++;
                            }
                            else if (status.Contains("resolved"))
                            {
                                resolvedScopeCases++;
                            }
                        }
                    }
                }
            }

            ViewBag.OfficerName = HttpContext.Session.GetString("OfficerName");
            ViewBag.OfficerRank = HttpContext.Session.GetString("OfficerRank");
            ViewBag.StationScope = stationScope;
            ViewBag.OfficerImage = HttpContext.Session.GetString("OfficerImage");

            ViewBag.TotalCases = totalScopeCases;
            ViewBag.ActiveCases = activeScopeCases;
            ViewBag.ResolvedCases = resolvedScopeCases;
            ViewBag.Complaints = assignedComplaints;

            return View("~/Views/OfficerPortal/Dashboard.cshtml");
        }

        // 🚪 GET: /OfficerPortal/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}