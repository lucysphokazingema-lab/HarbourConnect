using System;
using System.ComponentModel.DataAnnotations;

namespace APDP.Models
{
    public class Notification
    {
        [Key]
        public int NotificationID { get; set; }

        /// <summary>The ID of the user who receives this notification.</summary>
        [Required]
        public int UserID { get; set; }

        /// <summary>BoatOwner | Customer | Driver | TnpaAdmin</summary>
        [Required]
        [StringLength(20)]
        public string UserType { get; set; }

        [Required]
        [StringLength(500)]
        public string Message { get; set; }

        /// <summary>Optional link to navigate to when the notification is clicked.</summary>
        [StringLength(300)]
        public string Link { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
