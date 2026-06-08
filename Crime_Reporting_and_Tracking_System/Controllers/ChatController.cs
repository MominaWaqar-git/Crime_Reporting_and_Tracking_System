using Crime_Reporting_and_Tracking_System.Data;
using Crime_Reporting_and_Tracking_System.Models;
using CrimeReportingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
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
        // 1. MAIN CHAT WINDOW (Admin Sidebar & Active Chat Load)
        // ==========================================
        [HttpGet]
        public IActionResult ViewChat(string citizenPhone)
        {
            if (_context.GroupChats == null || _context.Complaints == null || _context.Users == null || _context.Officers == null)
            {
                return Content("Database tables properly mapped nahi hain DbContext mein.");
            }

            var sidebarList = (from gc in _context.GroupChats
                               where gc.IsDeleted == false
                               join comp in _context.Complaints on gc.ComplaintId equals comp.ID
                               join usr in _context.Users on comp.CitizenPhone equals usr.PhoneNumber into userJoin
                               from usr in userJoin.DefaultIfEmpty()
                               join off in _context.Officers on comp.CitizenPhone equals off.PhoneNumber into officerJoin
                               from off in officerJoin.DefaultIfEmpty()
                               group new { gc, comp, usr, off } by comp.CitizenPhone into g
                               select new
                               {
                                   CitizenPhone = g.Key,
                                   CitizenName = g.FirstOrDefault().usr != null ? g.FirstOrDefault().usr.FullName :
                                                (g.FirstOrDefault().off != null ? g.FirstOrDefault().off.Name : g.FirstOrDefault().comp.CitizenName),
                                   UserImage = g.FirstOrDefault().usr != null ? g.FirstOrDefault().usr.ProfileImage : null,
                                   OfficerImage = g.FirstOrDefault().off != null ? g.FirstOrDefault().off.ProfilePicturePath : null,
                                   ChatId = g.FirstOrDefault().gc.ChatId
                               }).ToList()
                               .Select(x => new
                               {
                                   x.CitizenPhone,
                                   x.CitizenName,
                                   ProfileImage = GetNormalizedImagePath(x.UserImage, x.OfficerImage),
                                   x.ChatId
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
                        .Where(m => m.ChatId == activeRoom.ChatId && m.IsRead == false && m.SenderType != "Admin" && m.SenderType != "System")
                        .ToList();

                    if (unreadMsgs.Any())
                    {
                        unreadMsgs.ForEach(m => m.IsRead = true);
                        _context.SaveChanges();
                    }

                    var currentUser = _context.Users.FirstOrDefault(u => u.PhoneNumber == citizenPhone);
                    var currentOfficer = _context.Officers.FirstOrDefault(o => o.PhoneNumber == citizenPhone);
                    var currentCitizen = _context.Complaints.FirstOrDefault(c => c.CitizenPhone == citizenPhone);

                    ViewBag.SelectedCitizenName = currentUser != null ? currentUser.FullName :
                                                 (currentOfficer != null ? currentOfficer.Name :
                                                 (currentCitizen != null ? currentCitizen.CitizenName : "Unknown"));
                    ViewBag.SelectedCitizenPhoneInfo = citizenPhone;
                }
            }

            return View(messagesToShow);
        }

        private static string GetNormalizedImagePath(string userImage, string officerImage)
        {
            string rawPath = "";
            if (!string.IsNullOrEmpty(userImage)) rawPath = userImage.Trim();
            else if (!string.IsNullOrEmpty(officerImage)) rawPath = officerImage.Trim();

            if (string.IsNullOrEmpty(rawPath)) return "/images/default-avatar.png";

            rawPath = rawPath.Replace("~", "").Replace("\\", "/");
            if (rawPath.StartsWith("/") || rawPath.StartsWith("http")) return rawPath;
            if (rawPath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase)) return "/" + rawPath;

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

                var verifiedRoom = _context.GroupChats.FirstOrDefault(g => g.ComplaintId == finalComplaintId && g.IsDeleted == false);

                if (verifiedRoom != null && verifiedRoom.ChatId > 0)
                {
                    var systemMsg = new ChatMessages
                    {
                        ChatId = verifiedRoom.ChatId,
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
        // 3. SEND MESSAGE (Admin Desk)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendMessage(int chatId, string citizenPhone, string messageText, string senderType, string senderName, IFormFile imageFile)
        {
            string finalMessageText = messageText?.Trim();

            // 1. Check karein kya user ne koi image file select ki hai?
            if (imageFile != null && imageFile.Length > 0)
            {
                try
                {
                    // wwwroot/uploads folder ka path set karein
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

                    // Agar uploads folder nahi bana hua toh pehle create karein
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // File ka ek unique naam banayein taake duplicate names overwrite na hon
                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(imageFile.FileName);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    // File ko folder me save karein
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        imageFile.CopyTo(fileStream);
                    }

                    // Frontend ki demand ke mutabiq MessageText me poora path save karein
                    finalMessageText = "/uploads/" + uniqueFileName;
                }
                catch (Exception ex)
                {
                    // Agar file save karte hue koi error aaye toh handle karein
                    return BadRequest("File upload failed: " + ex.Message);
                }
            }

            // 2. Agar na koi text likha hai aur na hi koi image upload hui hai, toh return kar jayein
            if (string.IsNullOrWhiteSpace(finalMessageText))
            {
                return BadRequest("Message cannot be empty.");
            }

            // 3. Database me entry insert karein
            var newMsg = new ChatMessages
            {
                ChatId = chatId,
                SenderType = senderType,
                SenderName = senderName,
                MessageText = finalMessageText, // Isme ab text ya image ka path chala jayega
                Timestamp = DateTime.Now,
                IsDeleted = false,
                IsRead = false
            };

            _context.ChatMessages.Add(newMsg);
            _context.SaveChanges();

             return Ok();
        }

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
        // 4. CITIZEN VIEW PORTAL (Admin & Officer Communication Hub)
        // ==========================================
        [HttpGet]
        public IActionResult CitizenPortal(string phone, string receiverType = "Admin")
        {
            if (string.IsNullOrEmpty(phone)) return RedirectToAction("Index", "Home");

            List<ChatMessages> msgs = new List<ChatMessages>();
            GroupChat room = null;

            if (receiverType == "Officer")
            {
                room = _context.GroupChats
                    .Where(g => g.IsDeleted == false)
                    .FirstOrDefault(g => _context.Complaints.Any(c => c.ID == g.ComplaintId && c.CitizenPhone == phone));

                var assignedOfficers = _context.Officers.Select(o => new { o.Name, o.PhoneNumber }).ToList();
                ViewBag.OfficerList = assignedOfficers;
            }
            else
            {
                room = _context.GroupChats
                    .Where(g => g.IsDeleted == false)
                    .FirstOrDefault(g => _context.Complaints.Any(c => c.ID == g.ComplaintId && c.CitizenPhone == phone));
            }

            if (room != null)
            {
                ViewBag.ActiveChatId = room.ChatId;
                msgs = _context.ChatMessages
                    .Where(m => m.ChatId == room.ChatId && m.IsDeleted == false)
                    .OrderBy(m => m.Timestamp)
                    .ToList();
            }

            ViewBag.CitizenPhone = phone;
            ViewBag.ReceiverType = receiverType;

            return View("CitizenChat", msgs);
        }

        [HttpGet]
        public IActionResult CitizenChat(string phone)
        {
            if (string.IsNullOrEmpty(phone))
            {
                phone = ViewBag.CitizenPhone ?? HttpContext.Session.GetString("UserPhone");
            }
            return RedirectToAction("CitizenPortal", new { phone = phone, receiverType = "Admin" });
        }

        // ==========================================
        // 5. CITIZEN SEND MESSAGE TO ADMIN / OFFICER
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendCitizenMessage(int chatId, string citizenPhone, string messageText, string receiverType = "Admin")
        {
            if (string.IsNullOrWhiteSpace(messageText))
            {
                return RedirectToAction("CitizenPortal", new { phone = citizenPhone, receiverType = receiverType });
            }

            if (chatId == 0 && !string.IsNullOrEmpty(citizenPhone))
            {
                var recoveryRoom = _context.GroupChats
                    .Where(g => g.IsDeleted == false)
                    .FirstOrDefault(g => _context.Complaints.Any(c => c.ID == g.ComplaintId && c.CitizenPhone == citizenPhone));

                if (recoveryRoom != null)
                {
                    chatId = recoveryRoom.ChatId;
                }
            }

            try
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
            catch (Exception ex)
            {
                return Content($"Database insertion failed: {ex.Message}. Check if ChatId {chatId} exists.");
            }

            return RedirectToAction("CitizenPortal", new { phone = citizenPhone, receiverType = receiverType });
        }

        [HttpGet]
        public IActionResult GetMessagesJson(int chatId)
        {
            var messages = _context.ChatMessages
                .Where(m => m.ChatId == chatId && m.IsDeleted == false)
                .OrderBy(m => m.Timestamp)
                .Select(m => new
                {
                    m.MessageId,
                    m.SenderName,
                    m.SenderType,
                    m.MessageText,
                    FormattedTime = m.Timestamp.ToString("hh:mm tt")
                }).ToList();

            return Json(messages);
        }

        // ==========================================
        // 6. OFFICER PORTAL CHAT ENGINE (Fixed Routing & Admin Support)
        // ==========================================
        [HttpGet]
        public IActionResult OfficerPortalDesk(string phone, string receiverType = "Citizen", string citizenPhone = "")
        {
            if (string.IsNullOrEmpty(phone))
            {
                return Content("Error: Officer Phone number missing hai! URL mein ?phone=NUMBER pass karein.");
            }

            var officer = _context.Officers.FirstOrDefault(o => o.PhoneNumber == phone);
            if (officer == null)
            {
                return Content("Database mein ye officer phone number nahi mila: " + phone);
            }

            // 1. Sidebar List: Active Citizens assigned to this Officer
            var activeCitizenRooms = (from gc in _context.GroupChats
                                      where gc.IsDeleted == false
                                      join comp in _context.Complaints on gc.ComplaintId equals comp.ID
                                      join assign in _context.ComplaintAssignments on comp.ID equals assign.ComplaintId
                                      join off in _context.Officers on assign.OfficerId equals off.Id
                                      where off.PhoneNumber == phone
                                      select new
                                      {
                                          CitizenPhone = comp.CitizenPhone,
                                          CitizenName = comp.CitizenName,
                                          ComplaintNo = comp.ID
                                      }).Distinct().ToList();

            ViewBag.ActiveCitizensForOfficer = activeCitizenRooms;
            ViewBag.OfficerName = officer.Name;
            ViewBag.OfficerPhone = phone;
            ViewBag.ReceiverType = receiverType;
            ViewBag.SelectedCitizenPhone = citizenPhone;

            List<ChatMessages> msgs = new List<ChatMessages>();
            GroupChat room = null;

            // 🔥 OFFICER TO ADMIN COMMUNICATION PIPELINE
            if (receiverType == "Admin")
            {
                // Officer directly Admin se baat karega (Using Officer's phone as the reference chat link)
                room = _context.GroupChats
                    .Where(g => g.IsDeleted == false)
                    .FirstOrDefault(g => _context.Complaints.Any(c => c.ID == g.ComplaintId && c.CitizenPhone == phone));
            }
            else // Default: Communicating with Selected Citizen
            {
                if (!string.IsNullOrEmpty(citizenPhone))
                {
                    room = _context.GroupChats
                        .Where(g => g.IsDeleted == false)
                        .FirstOrDefault(g => _context.Complaints.Any(c => c.ID == g.ComplaintId && c.CitizenPhone == citizenPhone));
                }
            }

            if (room != null)
            {
                ViewBag.ActiveChatId = room.ChatId;
                msgs = _context.ChatMessages
                    .Where(m => m.ChatId == room.ChatId && m.IsDeleted == false)
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                var unreadMsgs = _context.ChatMessages
                    .Where(m => m.ChatId == room.ChatId && m.IsRead == false && m.SenderType != "Officer")
                    .ToList();

                if (unreadMsgs.Any())
                {
                    unreadMsgs.ForEach(m => m.IsRead = true);
                    _context.SaveChanges();
                }
            }

            // Dynamic Title Setup for Chat Header
            if (receiverType == "Admin")
            {
                ViewBag.SelectedCitizenName = "Central Admin Desk";
                ViewBag.SelectedCitizenPhone = phone; // pipeline holder
            }
            else
            {
                var currentCitizen = _context.Complaints.FirstOrDefault(c => c.CitizenPhone == citizenPhone);
                ViewBag.SelectedCitizenName = currentCitizen != null ? currentCitizen.CitizenName : "Unknown Citizen";
            }

            // Hamesha 'OfficerChat.cshtml' view hi return hoga, duplicate route match ka khatma!
            return View("OfficerPortalDesk", msgs);
        }

        // Redirect action ka naam change kiya takay ambiguity khatam ho jaye
        [HttpGet]
        public IActionResult GoToOfficerChat(string officerPhone, string citizenPhone = "", string receiverType = "Citizen")
        {
            if (string.IsNullOrEmpty(officerPhone))
            {
                return Content("Error: officerPhone link mein empty hai.");
            }
            return RedirectToAction("OfficerPortalDesk", new { phone = officerPhone, receiverType = receiverType, citizenPhone = citizenPhone });
        }

        // ==========================================
        // 7. OFFICER SEND MESSAGE TO CITIZEN / ADMIN
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendOfficerMessage(int chatId, string officerPhone, string citizenPhone, string messageText, string receiverType = "Citizen", string senderName = "")
        {
            if (string.IsNullOrWhiteSpace(messageText))
            {
                return RedirectToAction("OfficerPortalDesk", new { phone = officerPhone, receiverType = receiverType, citizenPhone = citizenPhone });
            }

            // Admin target hai to path officerPhone hoga, warna citizenPhone
            string targetPhone = (receiverType == "Admin") ? officerPhone : citizenPhone;

            if (chatId == 0 && !string.IsNullOrEmpty(targetPhone))
            {
                var recoveryRoom = _context.GroupChats
                    .Where(g => g.IsDeleted == false)
                    .FirstOrDefault(g => _context.Complaints.Any(c => c.ID == g.ComplaintId && c.CitizenPhone == targetPhone));

                if (recoveryRoom != null)
                {
                    chatId = recoveryRoom.ChatId;
                }
            }

            try
            {
                var newMsg = new ChatMessages
                {
                    ChatId = chatId,
                    SenderType = "Officer",
                    SenderName = !string.IsNullOrEmpty(senderName) ? senderName : "Officer Terminal",
                    MessageText = messageText.Trim(),
                    Timestamp = DateTime.Now,
                    IsDeleted = false,
                    IsRead = false
                };
                _context.ChatMessages.Add(newMsg);
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                return Content($"Database insertion failed: {ex.Message}. ChatId {chatId} verified nahi hai.");
            }

            return RedirectToAction("OfficerPortalDesk", new { phone = officerPhone, receiverType = receiverType, citizenPhone = citizenPhone });
        }
    }
    }