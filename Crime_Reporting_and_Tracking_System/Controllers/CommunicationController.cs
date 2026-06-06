using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient; // Agar aap SqlConnection use kar rahe hain
using Microsoft.Extensions.Configuration;

public class CommunicationController : Controller
{
    private readonly string _connectionString;

    public CommunicationController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("CrimeDB");
    }

    // Chat interface load karne ke liye
    public IActionResult ChatList(int? complaintId)
    {
        // 1. Sidebar ke liye saari complaints fetch karein
        ViewBag.Complaints = GetComplaints();

        // 2. Agar koi specific case select ho, to uske messages fetch karein
        if (complaintId != null)
        {
            ViewBag.SelectedId = complaintId;
            ViewBag.Messages = GetMessages((int)complaintId);
        }
        else
        {
            ViewBag.SelectedId = 0;
            ViewBag.Messages = new List<dynamic>();
        }

        return View();
    }

    // Message send karne ke liye
    [HttpPost]
    public IActionResult SendMessage(int complaintId, string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                string query = "INSERT INTO Messages (ComplaintID, SenderType, MessageContent, Timestamp) VALUES (@cid, 'Officer', @msg, GETDATE())";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@cid", complaintId);
                cmd.Parameters.AddWithValue("@msg", message);

                con.Open();
                cmd.ExecuteNonQuery();
            }
        }
        return RedirectToAction("ChatList", new { complaintId = complaintId });
    }

    // Helper functions (Database access ke liye)
    private List<dynamic> GetComplaints()
    {
        var list = new List<dynamic>();
        using (SqlConnection con = new SqlConnection(_connectionString))
        {
            con.Open();
            SqlCommand cmd = new SqlCommand("SELECT ID, CrimeType, CitizenName FROM Complaints", con);
            SqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                list.Add(new { ID = rdr["ID"], CrimeType = rdr["CrimeType"], CitizenName = rdr["CitizenName"] });
            }
        }
        return list;
    }

    private List<dynamic> GetMessages(int id)
    {
        var list = new List<dynamic>();
        using (SqlConnection con = new SqlConnection(_connectionString))
        {
            con.Open();
            SqlCommand cmd = new SqlCommand("SELECT SenderType, MessageContent FROM Messages WHERE ComplaintID = @id ORDER BY Timestamp ASC", con);
            cmd.Parameters.AddWithValue("@id", id);
            SqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                list.Add(new { SenderType = rdr["SenderType"], MessageContent = rdr["MessageContent"] });
            }
        }
        return list;
    }
}