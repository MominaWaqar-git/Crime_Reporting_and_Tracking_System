using CrimeReportingSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Crime_Reporting_and_Tracking_System.Data;

namespace Crime_Reporting_and_Tracking_System.Controllers
{
    public class OfficersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OfficersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. INDEX: GET ALL OFFICERS FROM DATABASE
        public async Task<IActionResult> Index()
        {
            var officers = await _context.Officers.ToListAsync();
            return View(officers); // List view ko bhej rahe hain
        }

        // 2. CREATE: GET FORM PAGE
        public IActionResult Create()
        {
            return View(new Officer());
        }

        // 3. CREATE: POST DATA & STAY ON SAME PAGE WITH SUCCESS MSG
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Email,CNIC,PhoneNumber,Rank,Status,StationName,Address,ProfilePicturePath")] Officer officer, IFormFile photo)
        {
            // 🔥 PROFILE PICTURE VALIDATION FIX:
            // Agar photo select ki hui hai, toh purane validation error ko clear kar do
            if (photo != null && photo.Length > 0)
            {
                ModelState.Remove("ProfilePicturePath");
            }

            // ---- UNIQUE DATABASE CHECKS ----
            if (_context.Officers.Any(o => o.Email == officer.Email))
            {
                ModelState.AddModelError("Email", "This Official Email is already registered.");
            }
            if (_context.Officers.Any(o => o.CNIC == officer.CNIC))
            {
                ModelState.AddModelError("CNIC", "This CNIC Number is already registered.");
            }
            if (_context.Officers.Any(o => o.PhoneNumber == officer.PhoneNumber))
            {
                ModelState.AddModelError("PhoneNumber", "This Mobile Number is already registered.");
            }

            // 🔥 DEBUGGER: Agar ab bhi koi error bacha toh bataye ga
            if (!ModelState.IsValid)
            {
                var errors = string.Join(" | ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));

                ViewBag.DebugError = "Validation Failed! Reasons: " + errors;
                return View(officer);
            }

            // ---- AGAR VALIDATION PASSED HAI TOH DATABASE MEIN SAVE KAREIN ----
            if (ModelState.IsValid)
            {
                // Photo Upload Logic
                if (photo != null && photo.Length > 0)
                {
                    var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(photo.FileName);
                    var filePath = Path.Combine(folderPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await photo.CopyToAsync(stream);
                    }

                    // Model ki property ko file ka naam assign karein
                    officer.ProfilePicturePath = fileName;
                }

                // Save to Database Table
                _context.Add(officer);
                await _context.SaveChangesAsync();

                // Premium Success Message
                ViewBag.SuccessMessage = "Officer registered successfully within CrimeVision database registry!";

                ModelState.Clear();
                return View(new Officer());
            }

            return View(officer);
        }

        // 4. DETAILS
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var officer = await _context.Officers.FirstOrDefaultAsync(m => m.Id == id);
            if (officer == null) return NotFound();
            return View(officer);
        }

        // 5. EDIT: GET
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var officer = await _context.Officers.FindAsync(id);
            if (officer == null) return NotFound();
            return View(officer);
        }

        // 6. EDIT: POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Email,CNIC,PhoneNumber,Rank,Status,StationName,Address,ProfilePicturePath")] Officer officer, IFormFile? oldPhoto)
        {
            if (id != officer.Id)
            {
                return NotFound();
            }

            // 🔥 PROFILE PICTURE VALIDATION FIX FOR EDIT:
            // Agar nayi photo select ki hai ya purani pehle se majood hai, toh validation error clear karo
            if ((oldPhoto != null && oldPhoto.Length > 0) || !string.IsNullOrEmpty(officer.ProfilePicturePath))
            {
                ModelState.Remove("ProfilePicturePath");
            }

            // ---- UNIQUE DATABASE CHECKS (Bypass current user ID) ----
            // Check karein ke kisi DOOSRE officer ka email toh same nahi hai
            if (_context.Officers.Any(o => o.Email == officer.Email && o.Id != officer.Id))
            {
                ModelState.AddModelError("Email", "This Official Email is already assigned to another officer.");
            }

            // Check karein ke kisi DOOSRE officer ka CNIC toh same nahi hai
            if (_context.Officers.Any(o => o.CNIC == officer.CNIC && o.Id != officer.Id))
            {
                ModelState.AddModelError("CNIC", "This CNIC Number is already assigned to another officer.");
            }

            // Check karein ke kisi DOOSRE officer ka Phone toh same nahi hai
            if (_context.Officers.Any(o => o.PhoneNumber == officer.PhoneNumber && o.Id != officer.Id))
            {
                ModelState.AddModelError("PhoneNumber", "This Mobile Number is already assigned to another officer.");
            }

            // ---- VALIDATION FAILED WAALAY ACTIONS ----
            if (!ModelState.IsValid)
            {
                var errors = string.Join(" | ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));

                // Create ki tarah Edit par bhi top par red box error dikhane ke liye
                ViewBag.DebugError = "Update Failed! Reasons: " + errors;
                return View(officer);
            }

            // ---- AGAR VALIDATION PASS HO GAYI (SAVE TO DB) ----
            if (ModelState.IsValid)
            {
                try
                {
                    // Agar admin ne NEW photo select ki hai
                    if (oldPhoto != null && oldPhoto.Length > 0)
                    {
                        var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                        if (!Directory.Exists(folderPath))
                        {
                            Directory.CreateDirectory(folderPath);
                        }

                        // Nayi file ka unique naam banayein
                        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(oldPhoto.FileName);
                        var filePath = Path.Combine(folderPath, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await oldPhoto.CopyToAsync(stream);
                        }

                        // Purani picture file ko wwwroot se delete karna (Optional but good practice)
                        if (!string.IsNullOrEmpty(officer.ProfilePicturePath))
                        {
                            var oldFilePath = Path.Combine(folderPath, officer.ProfilePicturePath);
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                        }

                        // Model ko naya path assign karein
                        officer.ProfilePicturePath = fileName;
                    }

                    // Database Context state ko modified mark karein aur save karein
                    _context.Update(officer);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Officers.Any(e => e.Id == officer.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                // Successfully update hone ke baad wapas index list par bhej dein
                return RedirectToAction(nameof(Index));
            }

            return View(officer);
        } 

        // 7. DELETE
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var officer = await _context.Officers.FindAsync(id);
            if (officer != null)
            {
                _context.Officers.Remove(officer);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool OfficerExists(int id)
        {
            return _context.Officers.Any(e => e.Id == id);
        }
    }
}