using System;
using System.Linq;
using System.Web.Mvc;
using APDP.Models;

namespace APDP.Controllers
{
    public class NotificationsController : Controller
    {
        private HarbourConnectContext db = new HarbourConnectContext();

        // ─────────────────────────────────────────────
        // Helper: get current user ID and type from session
        // ─────────────────────────────────────────────

        private int? GetUserID()
        {
            if (Session["BoatOwnerID"] != null) return (int)Session["BoatOwnerID"];
            if (Session["CustomerID"]  != null) return (int)Session["CustomerID"];
            if (Session["DriverID"]    != null) return (int)Session["DriverID"];
            if (Session["TnpaAdminID"] != null) return (int)Session["TnpaAdminID"];
            return null;
        }

        private string GetUserType()
        {
            if (Session["BoatOwnerID"] != null) return "BoatOwner";
            if (Session["CustomerID"]  != null) return "Customer";
            if (Session["DriverID"]    != null) return "Driver";
            if (Session["TnpaAdminID"] != null) return "TnpaAdmin";
            return null;
        }

        // ─────────────────────────────────────────────
        // INDEX — list all notifications for current user
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult Index()
        {
            var userID   = GetUserID();
            var userType = GetUserType();

            if (userID == null || userType == null)
                return RedirectToAction("Index", "Home");

            // Mark all as read when the user opens notifications
            var unread = db.Notifications
                .Where(n => n.UserID == userID && n.UserType == userType && !n.IsRead)
                .ToList();
            foreach (var n in unread)
                n.IsRead = true;
            db.SaveChanges();

            var all = db.Notifications
                .Where(n => n.UserID == userID && n.UserType == userType)
                .OrderByDescending(n => n.CreatedAt)
                .ToList();

            return View(all);
        }

        // ─────────────────────────────────────────────
        // UNREAD COUNT — called via AJAX for the bell badge
        // ─────────────────────────────────────────────

        [HttpGet]
        public JsonResult UnreadCount()
        {
            var userID   = GetUserID();
            var userType = GetUserType();

            if (userID == null || userType == null)
                return Json(new { count = 0 }, JsonRequestBehavior.AllowGet);

            int count = db.Notifications
                .Count(n => n.UserID == userID && n.UserType == userType && !n.IsRead);

            return Json(new { count }, JsonRequestBehavior.AllowGet);
        }

        // ─────────────────────────────────────────────
        // MARK ONE AS READ and redirect to its link
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult Open(int? id)
        {
            if (id == null) return RedirectToAction("Index");

            var userID   = GetUserID();
            var userType = GetUserType();

            var notification = db.Notifications
                .FirstOrDefault(n => n.NotificationID == id
                                  && n.UserID == userID
                                  && n.UserType == userType);

            if (notification != null)
            {
                notification.IsRead = true;
                db.SaveChanges();

                if (!string.IsNullOrEmpty(notification.Link))
                    return Redirect(notification.Link);
            }

            return RedirectToAction("Index");
        }

        // ─────────────────────────────────────────────
        // CLEAR ALL — delete all notifications for this user
        // ─────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ClearAll()
        {
            var userID   = GetUserID();
            var userType = GetUserType();

            if (userID != null && userType != null)
            {
                var all = db.Notifications
                    .Where(n => n.UserID == userID && n.UserType == userType)
                    .ToList();
                db.Notifications.RemoveRange(all);
                db.SaveChanges();
            }

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
