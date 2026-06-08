using Crime_Reporting_and_Tracking_System.Data;
using Crime_Reporting_and_Tracking_System.Models;
using CrimeReportingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Crime_Reporting_and_Tracking_System.Controllers
{
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ChatController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. MAIN CHAT WINDOW (Sidebar & Active Chat Load)
        // ==========================================
        [HttpGet]
        public IActionResult ViewChat(string citizenPhone)
        {
            if (_context.GroupChats == null || _context.Complaints == null || _context.Users == null || _context.Officers == null)
            {
                return Content("Database tables properly mapped nahi hain DbContext mein.");
            }

            var rawChatRooms = _context.GroupChats.Where(g => g.IsDeleted == false).ToList();
            var complaintsList = _context.Complaints.ToList();
            var usersList = _context.Users.ToList();
            var officersList = _context.Officers.ToList();

            var sidebarList = (from gc in rawChatRooms
                               join comp in complaintsList on gc.ComplaintId equals comp.ID into compJoin
                               from comp in compJoin.DefaultIfEmpty()
                               where comp != null

                               join usr in usersList on comp.CitizenPhone equals usr.PhoneNumber into userJoin
                               from usr in userJoin.DefaultIfEmpty()

                               join off in officersList on comp.CitizenPhone equals off.PhoneNumber into officerJoin
                               from off in officerJoin.DefaultIfEmpty()

                               group new { gc, comp, usr, off } by comp.CitizenPhone into g
                               select new
                               {
                                   CitizenPhone = g.Key,
                                   CitizenName = g.FirstOrDefault().usr != null ? g.FirstOrDefault().usr.FullName :
                                                (g.FirstOrDefault().off != null ? g.FirstOrDefault().off.Name : g.FirstOrDefault().comp.CitizenName),

                                   // 🔥 FIXED: Real-time image normalization for backend sidebar grid
                                   ProfileImage = GetNormalizedImagePath(g.FirstOrDefault().usr, g.FirstOrDefault().off),

                                   ChatId = g.FirstOrDefault().gc.ChatId
                               }).ToList();

            ViewBag.ChatList = sidebarList;
            ViewBag.SelectedCitizenPhone = citizenPhone;

            List<ChatMessages> messagesToShow = new List<ChatMessages>();

            if (!string.IsNullOrEmpty(citizenPhone))
            {
                var activeRoom = _context.GroupChats
                    .Where(g => g.IsDeleted == false)
                    .FirstOrDefault(g => _context.Complaints.Any(c => c.ID == g.ComplaintId && c.CitizenPhone == citizenPhone));

                if (activeRoom != null)
                {
                    ViewBag.ActiveChatId = activeRoom.ChatId;

                    messagesToShow = _context.ChatMessages
                        .Where(m => m.ChatId == activeRoom.ChatId && m.IsDeleted == false)
                        .OrderBy(m => m.Timestamp)
                        .ToList();

                    var unreadMsgs = _context.ChatMessages
                        .Where(m => m.ChatId == activeRoom.ChatId && m.IsRead == false && m.SenderType != "Admin")
                        .ToList();

                    if (unreadMsgs.Any())
                    {
                        unreadMsgs.ForEach(m => m.IsRead = true);
                        _context.SaveChanges();
                    }

                    var currentCitizen = complaintsList.FirstOrDefault(c => c.CitizenPhone == citizenPhone);
                    var currentUser = usersList.FirstOrDefault(u => u.PhoneNumber == citizenPhone);
                    var currentOfficer = officersList.FirstOrDefault(o => o.PhoneNumber == citizenPhone);

                    ViewBag.SelectedCitizenName = currentUser != null ? currentUser.FullName :
                                                 (currentOfficer != null ? currentOfficer.Name :
                                                 (currentCitizen != null ? currentCitizen.CitizenName : "Unknown"));
                    ViewBag.SelectedCitizenPhoneInfo = citizenPhone;
                }
            }

            return View(messagesToShow);
        }

        // 🔥 HELPER METHOD: Profile picture path sanitizer engine
        private static string GetNormalizedImagePath(User usr, Officer off)
        {
            string rawPath = "";

            if (usr != null && !string.IsNullOrEmpty(usr.ProfileImage))
            {
                rawPath = usr.ProfileImage.Trim();
            }
            else if (off != null && !string.IsNullOrEmpty(off.ProfilePicturePath))
            {
                rawPath = off.ProfilePicturePath.Trim();
            }

            if (string.IsNullOrEmpty(rawPath))
            {
                return "/images/default-avatar.png";
            }

            // Remove legacy MVC path tokens
            rawPath = rawPath.Replace("~", "").Replace("\\", "/");

            if (rawPath.StartsWith("/") || rawPath.StartsWith("http"))
            {
                return rawPath;
            }

            if (rawPath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            {
                return "/" + rawPath;
            }

            return "/uploads/" + rawPath;
        }

        // ==========================================
        // 2. CREATE NEW CHAT (Direct Pipeline Entry)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateNewChat(string personName, string personPhone, bool isOfficer = false)
        {
            if (string.IsNullOrEmpty(personName) || string.IsNullOrEmpty(personPhone))
            {
                return RedirectToAction("ViewChat");
            }

            var existingComplaint = _context.Complaints.FirstOrDefault(c => c.CitizenPhone == personPhone);
            int finalComplaintId = 0;

            if (existingComplaint == null)
            {
                var autoComplaint = new Complaint
                {
                    CrimeType = isOfficer ? "Officer Communication" : "Direct Chat Reference",
                    IncidentDate = DateTime.Now,
                    Location = "Internal Police Portal",
                    Description = isOfficer ? $"Direct line with Officer {personName}." : "Directly initiated citizen chat link.",
                    Status = "Active",
                    CitizenName = personName,
                    CitizenPhone = personPhone
                };

                _context.Complaints.Add(autoComplaint);
                _context.SaveChanges();
                finalComplaintId = autoComplaint.ID;
            }
            else
            {
                finalComplaintId = existingComplaint.ID;
            }

            var existingRoom = _context.GroupChats.FirstOrDefault(g => g.ComplaintId == finalComplaintId);

            if (existingRoom != null)
            {
                existingRoom.IsDeleted = false;
                _context.SaveChanges();
            }
            else
            {
                var newRoom = new GroupChat
                {
                    ComplaintId = finalComplaintId,
                    CreatedDate = DateTime.Now,
                    IsDeleted = false
                };

                _context.GroupChats.Add(newRoom);
                _context.SaveChanges();

                if (newRoom.ChatId > 0)
                {
                    var systemMsg = new ChatMessages
                    {
                        ChatId = newRoom.ChatId,
                        SenderType = "System",
                        SenderName = "System Log",
                        MessageText = isOfficer ? $"Official channel setup for Officer {personName}." : $"Chat pipeline established for {personName}.",
                        Timestamp = DateTime.Now,
                        IsDeleted = false,
                        IsRead = true
                    };
                    _context.ChatMessages.Add(systemMsg);
                    _context.SaveChanges();
                }
            }

            return RedirectToAction("ViewChat", new { citizenPhone = personPhone });
        }

        // ==========================================
        // 3. SEND MESSAGE (Admin / Officers Desk)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendMessage(int chatId, string citizenPhone, string messageText, string senderType, string senderName)
        {
            if (!string.IsNullOrWhiteSpace(messageText))
            {
                var newMsg = new ChatMessages
                {
                    ChatId = chatId,
                    SenderType = senderType,
                    SenderName = senderName,
                    MessageText = messageText.Trim(),
                    Timestamp = DateTime.Now,
                    IsDeleted = false,
                    IsRead = false
                };

                _context.ChatMessages.Add(newMsg);
                _context.SaveChanges();
            }

            return RedirectToAction("ViewChat", new { citizenPhone = citizenPhone });
        }

        // ==========================================
        // 4. DELETE INDIVIDUAL MESSAGE (Soft Delete)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteMessage(int messageId, string citizenPhone)
        {
            var msg = _context.ChatMessages.FirstOrDefault(m => m.MessageId == messageId);
            if (msg != null)
            {
                msg.IsDeleted = true;
                _context.SaveChanges();
            }

            return RedirectToAction("ViewChat", new { citizenPhone = citizenPhone });
        }

        // ==========================================
        // 5. DELETE ENTIRE CHAT ROOM
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteChatRoom(int chatId, string citizenPhone)
        {
            var room = _context.GroupChats.FirstOrDefault(g => g.ChatId == chatId);
            if (room != null)
            {
                room.IsDeleted = true;
                _context.SaveChanges();
            }

            return RedirectToAction("ViewChat", new { citizenPhone = "" });
        }

        // ==========================================
        // 6. CITIZEN VIEW PORTAL (Core Route Engine)
        // ==========================================
        [HttpGet]
        public IActionResult CitizenPortal(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return RedirectToAction("Index", "Home");

            List<ChatMessages> msgs = new List<ChatMessages>();

            var room = _context.GroupChats
                .Where(g => g.IsDeleted == false)
                .FirstOrDefault(g => _context.Complaints.Any(c => c.ID == g.ComplaintId && c.CitizenPhone == phone));

            if (room != null)
            {
                ViewBag.ActiveChatId = room.ChatId;
                msgs = _context.ChatMessages
                    .Where(m => m.ChatId == room.ChatId && m.IsDeleted == false)
                    .OrderBy(m => m.Timestamp)
                    .ToList();
            }

            ViewBag.CitizenPhone = phone;
            return View("CitizenChat", msgs);
        }

        // ==========================================
        // 6b. 🔥 NEW ALIAS: To Support view dynamic tags (asp-action="CitizenChat")
        // ==========================================
        [HttpGet]
        public IActionResult CitizenChat(string phone)
        {
            // Session fallback if phone query key is dropped from view request context
            if (string.IsNullOrEmpty(phone))
            {
                phone = ViewBag.CitizenPhone ?? HttpContext.Session.GetString("UserPhone");
            }
            return RedirectToAction("CitizenPortal", new { phone = phone });
        }

        // ==========================================
        // 7. CITIZEN PORTAL SEND MESSAGE
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendCitizenMessage(int chatId, string citizenPhone, string messageText)
        {
            if (!string.IsNullOrWhiteSpace(messageText))
            {
                var newMsg = new ChatMessages
                {
                    ChatId = chatId,
                    SenderType = "Citizen",
                    SenderName = "Citizen Terminal",
                    MessageText = messageText.Trim(),
                    Timestamp = DateTime.Now,
                    IsDeleted = false,
                    IsRead = false
                };
                _context.ChatMessages.Add(newMsg);
                _context.SaveChanges();
            }
            return RedirectToAction("CitizenPortal", new { phone = citizenPhone });
        }

        // 1. Method ka naam "OfficerPortal" se "OfficerChat" kar diya
        [HttpGet]
        public IActionResult OfficerChat(string officerPhone)
        {
            // 1. Phone number check
            if (string.IsNullOrEmpty(officerPhone))
            {
                return Content("Error: Phone number missing hai!");
            }

            // 2. Database se Officer dhundo
            var officer = _context.Officers.FirstOrDefault(o => o.PhoneNumber == officerPhone);
            if (officer == null)
            {
                return Content("Database mein ye phone number nahi mila: " + officerPhone);
            }

            // 3. Messages ki list
            var msgs = new List<ChatMessages>();

            // 4. Room dhundo (Yahan aap filter laga sakte ho agar room assigned hai)
            var room = _context.GroupChats
                .Where(g => g.IsDeleted == false)
                .FirstOrDefault();

            if (room != null)
            {
                ViewBag.ActiveChatId = room.ChatId;
                msgs = _context.ChatMessages
                    .Where(m => m.ChatId == room.ChatId && m.IsDeleted == false)
                    .OrderBy(m => m.Timestamp)
                    .ToList();
            }

            // 5. Data View ko bhejo
            ViewBag.OfficerName = officer.Name;
            ViewBag.OfficerPhone = officer.PhoneNumber;

            return View("OfficerChat", msgs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendOfficerMessage(int chatId, string officerPhone, string messageText, string senderName)
        {
            if (!string.IsNullOrWhiteSpace(messageText))
            {
                var newMsg = new ChatMessages
                {
                    ChatId = chatId,
                    SenderType = "Officer",
                    SenderName = senderName,
                    MessageText = messageText.Trim(),
                    Timestamp = DateTime.Now,
                    IsDeleted = false,
                    IsRead = false
                };
                _context.ChatMessages.Add(newMsg);
                _context.SaveChanges();
            }

            // 3. Redirect action ko bhi update kar diya
            return RedirectToAction("OfficerChat", new { officerPhone = officerPhone });
        }
    }
}