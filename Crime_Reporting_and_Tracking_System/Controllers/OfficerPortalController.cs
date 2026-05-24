using Crime_Reporting_and_Tracking_System.Models;
using CrimeReportingSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq; // Count karne ke liye zaroori hai

namespace Crime_Reporting_and_Tracking_System.Controllers
{
    public class OfficerPortalController : Controller
    {
        private readonly IConfiguration _configuration;

        public OfficerPortalController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Login()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("OfficerCNIC")))
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(Officer model)
        {
            if (string.IsNullOrEmpty(model.CNIC))
            {
                ViewBag.Error = "Please enter your official CNIC Number.";
                return View(model);
            }

            string cleanCNIC = model.CNIC.Replace("-", "").Trim();
            string conString = _configuration.GetConnectionString("CrimeDB");

            using (SqlConnection con = new SqlConnection(conString))
            {
                string query = "SELECT * FROM Officers WHERE REPLACE(CNIC, '-', '') = @cnic";
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@cnic", cleanCNIC);
                    con.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            HttpContext.Session.SetString("OfficerId", reader["Id"].ToString());
                            HttpContext.Session.SetString("OfficerName", reader["Name"].ToString());
                            HttpContext.Session.SetString("OfficerCNIC", reader["CNIC"].ToString());
                            HttpContext.Session.SetString("OfficerRank", reader["Rank"].ToString());
                            HttpContext.Session.SetString("StationScope", reader["StationName"].ToString());
                            HttpContext.Session.SetString("OfficerImage", reader["ProfilePicturePath"]?.ToString() ?? "default-avatar.png");

                            return RedirectToAction("Dashboard");
                        }
                        else
                        {
                            ViewBag.Error = "No registered Officer found.";
                            return View(model);
                        }
                    }
                }
            }
        }

        [HttpGet]
        public IActionResult Dashboard()
        {
            string officerId = HttpContext.Session.GetString("OfficerId");
            if (string.IsNullOrEmpty(officerId)) return RedirectToAction("Login");

            List<dynamic> assignedComplaints = new List<dynamic>();
            int total = 0, active = 0, resolved = 0;
            string conString = _configuration.GetConnectionString("CrimeDB");

            using (SqlConnection con = new SqlConnection(conString))
            {
                // JOIN Query: Assignment table aur Complaints table ko link kiya
                string query = @"SELECT C.* FROM Complaints C 
                                 INNER JOIN ComplaintAssignments A ON C.ID = A.ComplaintId 
                                 WHERE A.OfficerId = @oid ORDER BY C.ID DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@oid", officerId);
                    con.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var c = new ExpandoObject() as IDictionary<string, object>;
                            c.Add("ID", reader["ID"]);
                            c.Add("CrimeType", reader["CrimeType"].ToString());
                            c.Add("CitizenName", reader["CitizenName"].ToString());
                            c.Add("IncidentDate", Convert.ToDateTime(reader["IncidentDate"]).ToString("dd/MM/yyyy"));
                            c.Add("Status", reader["Status"].ToString());
                            assignedComplaints.Add(c);

                            total++;
                            if (reader["Status"].ToString().Trim().ToLower() == "resolved") resolved++;
                            else active++;
                        }
                    }
                }
            }

            ViewBag.OfficerName = HttpContext.Session.GetString("OfficerName");
            ViewBag.OfficerRank = HttpContext.Session.GetString("OfficerRank");
            ViewBag.OfficerImage = HttpContext.Session.GetString("OfficerImage");
            ViewBag.StationScope = HttpContext.Session.GetString("StationScope");

            ViewBag.TotalCases = total;
            ViewBag.ActiveCases = active;
            ViewBag.ResolvedCases = resolved;
            ViewBag.Complaints = assignedComplaints;

            return View("~/Views/OfficerPortal/Dashboard.cshtml");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}