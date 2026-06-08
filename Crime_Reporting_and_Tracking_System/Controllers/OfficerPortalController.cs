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
using System.Linq;

namespace Crime_Reporting_and_Tracking_System.Controllers
{
    public class OfficerPortalController : Controller
    {
        private readonly IConfiguration _configuration;
        public OfficerPortalController(IConfiguration configuration) { _configuration = configuration; }

        #region Authentication & Dashboard
        public IActionResult Login() => !string.IsNullOrEmpty(HttpContext.Session.GetString("OfficerId")) ? RedirectToAction("Dashboard") : View();

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

        public IActionResult Dashboard()
        {
            string oid = HttpContext.Session.GetString("OfficerId");
            if (string.IsNullOrEmpty(oid)) return RedirectToAction("Login");

            List<dynamic> list = new List<dynamic>();
            int t = 0, a = 0, r = 0;
            string dbPhoneNumber = ""; // Fallback k liye database variable

            using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("CrimeDB")))
            {
                // Query mein Officers table join kiya taake PhoneNumber mil sake safely
                string query = @"SELECT C.*, O.PhoneNumber AS OffPhone 
                         FROM Complaints C 
                         INNER JOIN ComplaintAssignments A ON C.ID = A.ComplaintId 
                         INNER JOIN Officers O ON A.OfficerId = O.Id
                         WHERE A.OfficerId = @oid 
                         ORDER BY C.ID DESC";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@oid", oid);
                con.Open();
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    // Pehle loop mein hi officer ka phone number nikal ayega
                    if (string.IsNullOrEmpty(dbPhoneNumber))
                    {
                        dbPhoneNumber = reader["OffPhone"]?.ToString();
                    }

                    var c = new ExpandoObject() as IDictionary<string, object>;
                    c.Add("ID", reader["ID"]);
                    c.Add("CrimeType", reader["CrimeType"]);
                    c.Add("CitizenName", reader["CitizenName"]);
                    c.Add("Status", reader["Status"]);
                    c.Add("IncidentDate", Convert.ToDateTime(reader["IncidentDate"]).ToString("dd/MM/yyyy"));
                    list.Add(c);
                    t++;
                    if (reader["Status"].ToString().Trim().ToLower() == "resolved") r++; else a++;
                }
            }

            // 🔥 DUAL CHECK SAFETY: Agar Session khali hai to Direct Database wala phone number use hoga
            string finalPhone = HttpContext.Session.GetString("PhoneNumber");
            if (string.IsNullOrEmpty(finalPhone))
            {
                finalPhone = dbPhoneNumber;
            }

            ViewBag.OfficerPhone = finalPhone; // Ab ye kabhi null nahi hoga!
            ViewBag.OfficerName = HttpContext.Session.GetString("OfficerName");
            ViewBag.OfficerRank = HttpContext.Session.GetString("OfficerRank");
            ViewBag.OfficerImage = HttpContext.Session.GetString("OfficerImage");

            ViewBag.TotalCases = t;
            ViewBag.ActiveCases = a;
            ViewBag.ResolvedCases = r;
            ViewBag.Complaints = list;

            return View();
        }
        #endregion

        #region Case & Evidence
        public IActionResult CaseDetails(int id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("OfficerId"))) return RedirectToAction("Login");
            var complaint = new ExpandoObject() as IDictionary<string, object>;
            List<string> evidenceList = new List<string>();

            using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("CrimeDB")))
            {
                string q = "SELECT * FROM Complaints WHERE ID = @id";
                SqlCommand cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@id", id);
                con.Open();
                SqlDataReader r = cmd.ExecuteReader();
                if (r.Read())
                {
                    complaint.Add("ID", r["ID"]);
                    complaint.Add("CrimeType", r["CrimeType"]);
                    complaint.Add("IncidentDate", Convert.ToDateTime(r["IncidentDate"]).ToString("dd/MM/yyyy"));
                    complaint.Add("Location", r["Location"]);
                    complaint.Add("Description", r["Description"]);
                    complaint.Add("Status", r["Status"]);
                    complaint.Add("CitizenName", r["CitizenName"]);
                    complaint.Add("CitizenPhone", r["CitizenPhone"]);
                }
                r.Close();
                string evQ = "SELECT FilePath FROM Evidence WHERE ComplaintId = @id";
                SqlCommand evCmd = new SqlCommand(evQ, con);
                evCmd.Parameters.AddWithValue("@id", id);
                SqlDataReader evReader = evCmd.ExecuteReader();
                while (evReader.Read()) evidenceList.Add(evReader["FilePath"].ToString());
            }
            ViewBag.EvidenceFiles = evidenceList;
            return View(complaint);
        }

        [HttpPost]
        public IActionResult UploadEvidence(int complaintId, IFormFile evidenceFile)
        {
            if (evidenceFile != null && evidenceFile.Length > 0)
            {
                string fileName = Guid.NewGuid().ToString() + "_" + evidenceFile.FileName;
                string path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", fileName);
                using (var stream = new FileStream(path, FileMode.Create)) evidenceFile.CopyTo(stream);
                using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("CrimeDB")))
                {
                    string q = "INSERT INTO Evidence (ComplaintId, FilePath) VALUES (@cid, @path)";
                    SqlCommand cmd = new SqlCommand(q, con);
                    cmd.Parameters.AddWithValue("@cid", complaintId); cmd.Parameters.AddWithValue("@path", fileName);
                    con.Open(); cmd.ExecuteNonQuery();
                }
                TempData["Success"] = "Evidence uploaded successfully!";
            }
            return RedirectToAction("CaseDetails", new { id = complaintId });
        }
        #endregion

        public IActionResult Logout() { HttpContext.Session.Clear(); return RedirectToAction("Login"); }
    }
}