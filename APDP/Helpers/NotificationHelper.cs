using System;
using APDP.Models;

namespace APDP.Helpers
{
    /// <summary>
    /// Central helper for creating notifications.
    /// Call Notify() from any controller after an important action.
    /// Every notification is shown in the app (bell icon) and also emailed to the user.
    /// </summary>
    public static class NotificationHelper
    {
        /// <summary>
        /// Saves a new notification for a user and queues the matching email.
        /// </summary>
        /// <param name="db">The current DB context.</param>
        /// <param name="userID">ID of the user who should receive it.</param>
        /// <param name="userType">BoatOwner | Customer | Driver | TnpaAdmin</param>
        /// <param name="message">The notification message.</param>
        /// <param name="link">Optional URL to navigate to on click.</param>
        public static void Notify(HarbourConnectContext db,
                                  int    userID,
                                  string userType,
                                  string message,
                                  string link = null)
        {
            db.Notifications.Add(new Notification
            {
                UserID    = userID,
                UserType  = userType,
                Message   = message,
                Link      = link,
                IsRead    = false,
                CreatedAt = DateTime.Now
            });
            // Caller is responsible for calling db.SaveChanges()

            if (EmailSender.IsEnabled)
            {
                string email, name;
                if (TryGetRecipient(db, userID, userType, out email, out name))
                    EmailSender.Queue(email, name, BuildSubject(message), message, link);
            }
        }

        private static bool TryGetRecipient(HarbourConnectContext db, int userID, string userType,
                                            out string email, out string name)
        {
            email = name = null;
            switch (userType)
            {
                case "Customer":
                    var c = db.Customers.Find(userID);
                    if (c != null) { email = c.Email; name = c.FullName; }
                    break;
                case "BoatOwner":
                    var o = db.BoatOwners.Find(userID);
                    if (o != null) { email = o.Email; name = o.FullName; }
                    break;
                case "Driver":
                    var d = db.Drivers.Find(userID);
                    if (d != null) { email = d.Email; name = d.FullName; }
                    break;
                case "TnpaAdmin":
                    var a = db.TnpaAdmins.Find(userID);
                    if (a != null) { email = a.Email; name = a.FullName; }
                    break;
            }
            return !string.IsNullOrWhiteSpace(email);
        }

        /// <summary>First sentence of the message, kept short, as the email subject.</summary>
        private static string BuildSubject(string message)
        {
            string text = (message ?? "").Trim();
            int end = text.IndexOfAny(new[] { '.', '!', '?' });
            if (end > 0) text = text.Substring(0, end + 1);
            if (text.Length > 80) text = text.Substring(0, 77) + "...";
            return "HarbourConnect: " + text;
        }
    }
}
