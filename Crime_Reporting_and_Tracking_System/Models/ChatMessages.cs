using System;
using System.ComponentModel.DataAnnotations;

namespace Crime_Reporting_and_Tracking_System.Models
{
    public class ChatMessages
    {
        [Key]
        public int MessageId { get; set; }
        public int ChatId { get; set; }
        public string SenderType { get; set; } // 'Admin', 'Officer', 'Citizen', 'System'
        public string SenderName { get; set; }
        public string MessageText { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;
        public bool IsRead { get; set; } = false;
    }
}