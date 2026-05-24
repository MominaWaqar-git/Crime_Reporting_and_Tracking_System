using Crime_Reporting_and_Tracking_System.Models;
using CrimeReportingSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
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
                            string status = reader["Status"]?.ToString() ?? "Active";
                            if (status.ToLower() != "active")
                            {
                                ViewBag.Error = "Your account is deactivated by the Admin.";
                                return View(model);
                            }

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
                            ViewBag.Error = "No registered Officer found with this CNIC.";
                            return View(model);
                        }
                    }
                }
            }
        }

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
            string conString = _configuration.GetConnectionString("CrimeDB");

            using (SqlConnection con = new SqlConnection(conString))
            {
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
                        }
                    }
                }
            }

            ViewBag.OfficerName = HttpContext.Session.GetString("OfficerName");
            ViewBag.StationScope = stationScope;
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