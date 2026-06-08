using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Crime_Reporting_and_Tracking_System.Data;
using Crime_Reporting_and_Tracking_System.Models;
using System.Linq;
using System;
using System.Collections.Generic;

namespace Crime_Reporting_and_Tracking_System.Controllers
{
    public class OfficerChatController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OfficerChatController(ApplicationDbContext context) => _context = context;

        [HttpGet]
        public IActionResult Conversation(string citizenPhone)
        {
            // 1. Sidebar List: Join use kiya taake Navigation Property ki zarurat na pade
            ViewBag.ChatList = _context.GroupChats
                .Join(_context.Complaints,
                      gc => gc.ComplaintId,
                      comp => comp.ID,
                      (gc, comp) => new { gc, comp })
                .Where(x => x.gc.IsDeleted == false)
                .Select(x => new {
                    x.gc.ChatId,
                    x.comp.CitizenName,
                    x.comp.CitizenPhone
                }).ToList();

            // 2. Active Chat Messages Load karna
            var messages = new List<ChatMessages>();

            if (!string.IsNullOrEmpty(citizenPhone))
            {
                // Yahan bhi Join use kiya taake Error na aaye
                var activeRoomData = _context.GroupChats
                    .Join(_context.Complaints,
                          gc => gc.ComplaintId,
                          comp => comp.ID,
                          (gc, comp) => new { gc, comp })
                    .FirstOrDefault(x => x.comp.CitizenPhone == citizenPhone && x.gc.IsDeleted == false);

                if (activeRoomData != null)
                {
                    ViewBag.ActiveChatId = activeRoomData.gc.ChatId;
                    ViewBag.SelectedCitizenPhone = citizenPhone;
                    ViewBag.SelectedCitizenName = activeRoomData.comp.CitizenName;

                    messages = _context.ChatMessages
                        .Where(m => m.ChatId == activeRoomData.gc.ChatId && m.IsDeleted == false)
                        .OrderBy(m => m.Timestamp)
                        .ToList();
                }
            }

            return View(messages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendOfficerMessage(int chatId, string citizenPhone, string messageText)
        {
            if (string.IsNullOrWhiteSpace(messageText))
            {
                return RedirectToAction("Conversation", new { citizenPhone = citizenPhone });
            }

            string officerName = HttpContext.Session.GetString("OfficerName") ?? "Duty Officer";

            var newMessage = new ChatMessages
            {
                ChatId = chatId,
                SenderType = "Officer",
                SenderName = officerName,
                MessageText = messageText.Trim(),
                Timestamp = DateTime.Now,
                IsRead = false,
                IsDeleted = false
            };

            _context.ChatMessages.Add(newMessage);
            _context.SaveChanges();

            return RedirectToAction("Conversation", new { citizenPhone = citizenPhone });
        }
    }
}