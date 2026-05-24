using Crime_Reporting_and_Tracking_System.Models;
using CrimeReportingSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;

namespace Crime_Reporting_and_Tracking_System.Controllers
{
    public class OfficerPortalController : Controller
    {
        private readonly IConfiguration _configuration;

        public OfficerPortalController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        #region Authentication
        public IActionResult Login()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("OfficerId"))) return RedirectToAction("Dashboard");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(Officer model)
        {
            if (string.IsNullOrEmpty(model.CNIC)) { ViewBag.Error = "Please enter CNIC."; return View(model); }

            string cleanCNIC = model.CNIC.Replace("-", "").Trim();
            using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("CrimeDB")))
            {
                string query = "SELECT * FROM Officers WHERE REPLACE(CNIC, '-', '') = @cnic";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@cnic", cleanCNIC);
                con.Open();
                SqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    HttpContext.Session.SetString("OfficerId", reader["Id"].ToString());
                    HttpContext.Session.SetString("OfficerName", reader["Name"].ToString());
                    HttpContext.Session.SetString("OfficerRank", reader["Rank"].ToString());
                    HttpContext.Session.SetString("OfficerImage", reader["ProfilePicturePath"]?.ToString() ?? "default-avatar.png");
                    return RedirectToAction("Dashboard");
                }
            }
            ViewBag.Error = "Invalid CNIC.";
            return View(model);
        }
        #endregion

        #region Dashboard Features
        public IActionResult Dashboard()
        {
            string officerId = HttpContext.Session.GetString("OfficerId");
            if (string.IsNullOrEmpty(officerId)) return RedirectToAction("Login");

            List<dynamic> assignedComplaints = new List<dynamic>();
            int total = 0, active = 0, resolved = 0;

            using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("CrimeDB")))
            {
                string query = @"SELECT C.* FROM Complaints C 
                                 INNER JOIN ComplaintAssignments A ON C.ID = A.ComplaintId 
                                 WHERE A.OfficerId = @oid ORDER BY C.ID DESC";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@oid", officerId);
                con.Open();
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var c = new ExpandoObject() as IDictionary<string, object>;
                    c.Add("ID", reader["ID"]);
                    c.Add("CrimeType", reader["CrimeType"]);
                    c.Add("CitizenName", reader["CitizenName"]);
                    c.Add("IncidentDate", Convert.ToDateTime(reader["IncidentDate"]).ToString("dd/MM/yyyy"));
                    c.Add("Status", reader["Status"]);
                    assignedComplaints.Add(c);

                    total++;
                    if (reader["Status"].ToString().Trim().ToLower() == "resolved") resolved++;
                    else active++;
                }
            }
            ViewBag.OfficerName = HttpContext.Session.GetString("OfficerName");
            ViewBag.OfficerRank = HttpContext.Session.GetString("OfficerRank");
            ViewBag.OfficerImage = HttpContext.Session.GetString("OfficerImage");
            ViewBag.TotalCases = total;
            ViewBag.ActiveCases = active;
            ViewBag.ResolvedCases = resolved;
            ViewBag.Complaints = assignedComplaints;
            return View();
        }

        [HttpPost]
        public IActionResult UpdateStatus(int complaintId, string newStatus)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("OfficerId"))) return RedirectToAction("Login");

            using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("CrimeDB")))
            {
                string query = "UPDATE Complaints SET Status = @status WHERE ID = @id";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@status", newStatus);
                cmd.Parameters.AddWithValue("@id", complaintId);
                con.Open();
                cmd.ExecuteNonQuery();
            }
            TempData["Success"] = "Status updated successfully!";
            return RedirectToAction("Dashboard");
        }
        #endregion

        #region Case & Evidence Features
        public IActionResult CaseDetails(int id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("OfficerId"))) return RedirectToAction("Login");

            var complaint = new ExpandoObject() as IDictionary<string, object>;
            using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("CrimeDB")))
            {
                string query = "SELECT * FROM Complaints WHERE ID = @id";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@id", id);
                con.Open();
                SqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    complaint.Add("ID", reader["ID"]);
                    complaint.Add("CrimeType", reader["CrimeType"]);
                    complaint.Add("IncidentDate", Convert.ToDateTime(reader["IncidentDate"]).ToString("dd/MM/yyyy"));
                    complaint.Add("Location", reader["Location"]);
                    complaint.Add("Description", reader["Description"]);
                    complaint.Add("Status", reader["Status"]);
                    complaint.Add("CitizenName", reader["CitizenName"]);
                    complaint.Add("CitizenPhone", reader["CitizenPhone"]);
                }
            }
            return View(complaint);
        }

        [HttpPost]
        public IActionResult UploadEvidence(int complaintId, IFormFile evidenceFile)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("OfficerId"))) return RedirectToAction("Login");

            if (evidenceFile != null && evidenceFile.Length > 0)
            {
                string fileName = Guid.NewGuid().ToString() + "_" + evidenceFile.FileName;
                string path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", fileName);
                using (var stream = new FileStream(path, FileMode.Create)) { evidenceFile.CopyTo(stream); }

                using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("CrimeDB")))
                {
                    string query = "INSERT INTO Evidence (ComplaintId, FilePath) VALUES (@cid, @path)";
                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@cid", complaintId);
                    cmd.Parameters.AddWithValue("@path", fileName);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
                TempData["Success"] = "Evidence uploaded successfully!";
            }
            return RedirectToAction("CaseDetails", new { id = complaintId });
        }
        #endregion

        public IActionResult Logout() { HttpContext.Session.Clear(); return RedirectToAction("Login"); }
    }
}