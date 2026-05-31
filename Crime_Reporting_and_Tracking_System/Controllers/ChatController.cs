using Crime_Reporting_and_Tracking_System.Data;
using Crime_Reporting_and_Tracking_System.Models;
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
            // Security check taake context null hone par crash na ho
            if (_context.GroupChats == null || _context.Complaints == null || _context.Users == null || _context.Officers == null)
            {
                return Content("Database tables properly mapped nahi hain DbContext mein.");
            }

            // Data ko memory mein le rahe hain taake EF Core complex queries par translate error na de
            var rawChatRooms = _context.GroupChats.Where(g => g.IsDeleted == false).ToList();
            var complaintsList = _context.Complaints.ToList();
            var usersList = _context.Users.ToList();
            var officersList = _context.Officers.ToList();

            // Clean Sidebar list building
            var sidebarList = (from gc in rawChatRooms
                               join comp in complaintsList on gc.ComplaintId equals comp.ID into compJoin
                               from comp in compJoin.DefaultIfEmpty()

                               where comp != null // Sirf valid mapped complaints/chats uthein

                               // Users (Citizen) table se match karein phone number
                               join usr in usersList on comp.CitizenPhone equals usr.PhoneNumber into userJoin
                               from usr in userJoin.DefaultIfEmpty()

                               group new { gc, comp, usr } by comp.CitizenPhone into g
                               select new
                               {
                                   CitizenPhone = g.Key,
                                   // Agar Users table mein naam hai toh wo, nahi toh Complaint wala naam
                                   CitizenName = g.FirstOrDefault().usr != null
                                                  ? g.FirstOrDefault().usr.FullName
                                                  : g.FirstOrDefault().comp.CitizenName,

                                   // AVATAR FIX: Agar user ki image hai toh wo, nahi toh default avatar path
                                   ProfileImage = (g.FirstOrDefault().usr != null && !string.IsNullOrEmpty(g.FirstOrDefault().usr.ProfileImage))
                                                  ? g.FirstOrDefault().usr.ProfileImage
                                                  : "/images/default-avatar.png",

                                   ChatId = g.FirstOrDefault().gc.ChatId
                               }).ToList();

            ViewBag.ChatList = sidebarList;
            ViewBag.SelectedCitizenPhone = citizenPhone;

            List<ChatMessages> messagesToShow = new List<ChatMessages>();

            // Agar koi chat selected hai, toh uske messages load karein
            if (!string.IsNullOrEmpty(citizenPhone))
            {
                var activeRoom = _context.GroupChats
                    .Where(g => g.IsDeleted == false)
                    .FirstOrDefault(g => _context.Complaints.Any(c => c.ID == g.ComplaintId && c.CitizenPhone == citizenPhone));

                if (activeRoom != null)
                {
                    ViewBag.ActiveChatId = activeRoom.ChatId;

                    // Messages fetch karein jo delete nahi hue
                    messagesToShow = _context.ChatMessages
                        .Where(m => m.ChatId == activeRoom.ChatId && m.IsDeleted == false)
                        .OrderBy(m => m.Timestamp)
                        .ToList();

                    // Unread messages ko Read (Seen) mark karein
                    var unreadMsgs = _context.ChatMessages
                        .Where(m => m.ChatId == activeRoom.ChatId && m.IsRead == false && m.SenderType != "Admin")
                        .ToList();

                    if (unreadMsgs.Any())
                    {
                        unreadMsgs.ForEach(m => m.IsRead = true);
                        _context.SaveChanges();
                    }

                    // Header par select kiye gaye bande ka naam dikhane ke liye
                    var currentCitizen = complaintsList.FirstOrDefault(c => c.CitizenPhone == citizenPhone);
                    var currentUser = usersList.FirstOrDefault(u => u.PhoneNumber == citizenPhone);

                    ViewBag.SelectedCitizenName = currentUser != null ? currentUser.FullName : (currentCitizen != null ? currentCitizen.CitizenName : "Unknown");
                    ViewBag.SelectedCitizenPhoneInfo = citizenPhone;
                }
            }

            return View(messagesToShow);
        }

        // ==========================================
        // 2. CREATE NEW CHAT (Sirf Name aur Phone se Entry)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateNewChat(string personName, string personPhone, bool isOfficer = false)
        {
            if (string.IsNullOrEmpty(personName) || string.IsNullOrEmpty(personPhone))
            {
                return RedirectToAction("ViewChat");
            }

            // Step 1: Database constraint se bachne ke liye link banayein
            var existingComplaint = _context.Complaints.FirstOrDefault(c => c.CitizenPhone == personPhone);
            int finalComplaintId = 0;

            if (existingComplaint == null)
            {
                // Agar Officer hai toh description mein 'Officer Chat Pipeline' likha aayega
                var autoComplaint = new Complaint
                {
                    CrimeType = isOfficer ? "Officer Communication" : "Direct Chat Reference",
                    IncidentDate = DateTime.Now,
                    Location = "Internal Police Portal",
                    Description = isOfficer ? $"Direct line with Officer {personName}." : "Directly initiated citizen chat link.",
                    Status = "Active",
                    CitizenName = personName,
                    CitizenPhone = personPhone // Idhar officer ya citizen ka phone number map ho jayega
                };

                _context.Complaints.Add(autoComplaint);
                _context.SaveChanges(); // SQL Server automatic unique ID generate kar dega
                finalComplaintId = autoComplaint.ID;
            }
            else
            {
                finalComplaintId = existingComplaint.ID;
            }

            // Step 2: Chat room create ya reactivate karein
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
                msg.IsDeleted = true; // Table column IsDeleted BIT matching
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
        // 6. CITIZEN VIEW PORTAL (User Dashboard side chat)
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
    }
}