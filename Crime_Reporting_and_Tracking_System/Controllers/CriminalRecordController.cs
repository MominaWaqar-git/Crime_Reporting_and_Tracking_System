using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System;

// Criminal Model (Isay controller ke neeche ya alag file mein rakh sakte hain)
public class Criminal
{
    public int ID { get; set; }
    public string Name { get; set; }
    public string History { get; set; }
    public string Status { get; set; }
}

public class CriminalRecordController : Controller
{
    // Aapke SSMS wale database naam ke mutabiq connection string
    private readonly string _connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=CrimeVisionDB;Trusted_Connection=True;TrustServerCertificate=True;";

    // READ: Sabhi criminals ki list dikhane ke liye
    public IActionResult Index()
    {
        List<Criminal> criminals = new List<Criminal>();
        using (SqlConnection con = new SqlConnection(_connectionString))
        {
            string query = "SELECT CriminalID, FullName, CrimeHistory, Status FROM Criminals";
            SqlCommand cmd = new SqlCommand(query, con);
            con.Open();
            SqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                criminals.Add(new Criminal
                {
                    ID = Convert.ToInt32(rdr["CriminalID"]),
                    Name = rdr["FullName"].ToString(),
                    History = rdr["CrimeHistory"].ToString(),
                    Status = rdr["Status"].ToString()
                });
            }
        }
        return View(criminals);
    }

    // CREATE: Form dikhane ke liye
    public IActionResult Add() => View();

    // CREATE: Data save karne ke liye
    [HttpPost]
    public IActionResult Add(string FullName, string CrimeHistory, string Status)
    {
        using (SqlConnection con = new SqlConnection(_connectionString))
        {
            string query = "INSERT INTO Criminals (FullName, CrimeHistory, Status) VALUES (@Name, @History, @Status)";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Name", FullName);
            cmd.Parameters.AddWithValue("@History", CrimeHistory);
            cmd.Parameters.AddWithValue("@Status", Status);
            con.Open();
            cmd.ExecuteNonQuery();
        }
        return RedirectToAction("Index");
    }

    // DELETE: Record hatane ke liye
    public IActionResult Delete(int id)
    {
        using (SqlConnection con = new SqlConnection(_connectionString))
        {
            string query = "DELETE FROM Criminals WHERE CriminalID = @id";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@id", id);
            con.Open();
            cmd.ExecuteNonQuery();
        }
        return RedirectToAction("Index");
    }
}