using System;
using System.Linq;
using System.Web.Mvc;
using APDP.Helpers;
using APDP.Models;

namespace APDP.Controllers
{
    public class TnpaController : Controller
    {
        private HarbourConnectContext db = new HarbourConnectContext();

        // ─────────────────────────────────────────────
        // LOGIN
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult Login()
        {
            if (Session["TnpaAdminID"] != null)
                return RedirectToAction("Dashboard");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password)
        {
            var admin = db.TnpaAdmins.FirstOrDefault(x => x.Email == email);

            bool valid = admin != null && PasswordHasher.Verify(password, admin.PasswordHash);
            if (admin == null)
                PasswordHasher.VerifyDummy(password);

            if (valid)
            {
                // Clear any other portal sessions first
                Session.Remove("BoatOwnerID");  Session.Remove("BoatOwnerName");
                Session.Remove("CustomerID");   Session.Remove("CustomerName");
                Session.Remove("DriverID");     Session.Remove("DriverName");

                Session["TnpaAdminID"]   = admin.TnpaAdminID;
                Session["TnpaAdminName"] = admin.FullName;
                Session["UserType"]      = "TnpaAdmin";
                return RedirectToAction("Dashboard");
            }

            ViewBag.ErrorMessage = "Invalid email or password. Please try again.";
            return View();
        }

        // ─────────────────────────────────────────────
        // LOGOUT
        // ─────────────────────────────────────────────

        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Login");
        }

        // ─────────────────────────────────────────────
        // DASHBOARD
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult Dashboard()
        {
            if (Session["TnpaAdminID"] == null)
                return RedirectToAction("Login");

            ViewBag.PendingCount  = db.Boats.Count(b => b.Status == BoatStatus.Pending);
            ViewBag.ApprovedCount = db.Boats.Count(b => b.Status == BoatStatus.Approved
                                                     || b.Status == BoatStatus.AwaitingPayment
                                                     || b.Status == BoatStatus.Active);
            ViewBag.RejectedCount = db.Boats.Count(b => b.Status == BoatStatus.Rejected);
            ViewBag.TotalOwners   = db.BoatOwners.Count();

            return View();
        }

        // ─────────────────────────────────────────────
        // PENDING BOATS
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult PendingBoats()
        {
            if (Session["TnpaAdminID"] == null)
                return RedirectToAction("Login");

            var boats = db.Boats
                .Include("BoatOwner")
                .Where(b => b.Status == BoatStatus.Pending)
                .OrderBy(b => b.DateAdded)
                .ToList();

            return View(boats);
        }

        // ─────────────────────────────────────────────
        // ALL BOATS
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult AllBoats()
        {
            if (Session["TnpaAdminID"] == null)
                return RedirectToAction("Login");

            var boats = db.Boats
                .Include("BoatOwner")
                .OrderByDescending(b => b.DateAdded)
                .ToList();

            return View(boats);
        }

        // ─────────────────────────────────────────────
        // BOAT DETAILS
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult BoatDetails(int? id)
        {
            if (Session["TnpaAdminID"] == null)
                return RedirectToAction("Login");

            if (id == null)
            {
                TempData["ErrorMessage"] = "No boat ID was specified.";
                return RedirectToAction("PendingBoats");
            }

            var boat = db.Boats
                .Include("BoatOwner")
                .Include("Extras")
                .FirstOrDefault(b => b.BoatID == id);

            if (boat == null)
            {
                TempData["ErrorMessage"] = "Boat not found.";
                return RedirectToAction("PendingBoats");
            }

            ViewBag.Week = BoatAvailability.WeekFor(db, boat);
            return View(boat);
        }

        // ─────────────────────────────────────────────
        // APPROVE
        // ─────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(int? id)
        {
            if (Session["TnpaAdminID"] == null)
                return RedirectToAction("Login");

            if (id == null)
                return RedirectToAction("PendingBoats");

            var boat = db.Boats.Find(id);
            if (boat != null && boat.Status != BoatStatus.Pending)
            {
                TempData["ErrorMessage"] = $"Boat '{boat.BoatName}' is not pending review.";
            }
            else if (boat != null && boat.RegistrationPaidDate != null)
            {
                // Re-approval after the owner changed key details: the fee was already paid.
                boat.Status          = BoatStatus.Active;
                boat.RejectionReason = null;
                db.SaveChanges();

                NotificationHelper.Notify(db,
                    boat.BoatOwnerID,
                    "BoatOwner",
                    $"TNPA has re-approved the changes to '{boat.BoatName}'. It is active and visible to customers again.",
                    "/BoatOwner/MyBoats");
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Boat '{boat.BoatName}' has been re-approved and is active again.";
            }
            else if (boat != null)
            {
                boat.Status          = BoatStatus.AwaitingPayment;
                boat.RejectionReason = null;
                db.SaveChanges();

                // Notify boat owner
                NotificationHelper.Notify(db,
                    boat.BoatOwnerID,
                    "BoatOwner",
                    $"Your boat '{boat.BoatName}' has been approved by TNPA! Please pay the registration fee to activate it.",
                    "/BoatOwner/PayRegistrationFee/" + boat.BoatID);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Boat '{boat.BoatName}' has been approved. The boat owner will now be prompted to pay the registration fee.";
            }

            return RedirectToAction("PendingBoats");
        }

        // ─────────────────────────────────────────────
        // REJECT
        // ─────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reject(int? id, string rejectionReason)
        {
            if (Session["TnpaAdminID"] == null)
                return RedirectToAction("Login");

            if (id == null)
                return RedirectToAction("PendingBoats");

            var boat = db.Boats.Find(id);
            if (boat != null)
            {
                boat.Status          = BoatStatus.Rejected;
                boat.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason)
                    ? "Rejected by TNPA Admin."
                    : rejectionReason;
                db.SaveChanges();

                // Notify boat owner
                NotificationHelper.Notify(db,
                    boat.BoatOwnerID,
                    "BoatOwner",
                    $"Your boat '{boat.BoatName}' was rejected by TNPA. Reason: {boat.RejectionReason}",
                    "/BoatOwner/MyBoats");
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Boat '{boat.BoatName}' has been rejected.";
            }

            return RedirectToAction("PendingBoats");
        }

        // ─────────────────────────────────────────────
        // REMOVE BOAT
        // ─────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveBoat(int? id, string removalReason)
        {
            if (Session["TnpaAdminID"] == null)
                return RedirectToAction("Login");

            if (id == null)
                return RedirectToAction("AllBoats");

            if (string.IsNullOrWhiteSpace(removalReason))
            {
                TempData["ErrorMessage"] = "A reason is required before removing a boat.";
                return RedirectToAction("BoatDetails", new { id });
            }

            var boat = db.Boats.Find(id);
            if (boat == null)
            {
                TempData["ErrorMessage"] = "Boat not found.";
                return RedirectToAction("AllBoats");
            }

            if (boat.Status == BoatStatus.Removed)
            {
                TempData["ErrorMessage"] = $"Boat '{boat.BoatName}' has already been removed.";
                return RedirectToAction("AllBoats");
            }

            string boatName = boat.BoatName;
            string reason   = removalReason.Trim();
            if (reason.Length > 500) reason = reason.Substring(0, 500);

            // The boat is taken off the platform but not deleted, so bookings,
            // payments and ratings stay on record for customers, owners and audit.
            boat.Status          = BoatStatus.Removed;
            boat.RejectionReason = reason;

            // 1. Unassign any drivers pointing to this boat
            var assignedDrivers = db.Drivers.Where(d => d.AssignedBoatID == id).ToList();
            foreach (var driver in assignedDrivers)
                driver.AssignedBoatID = null;

            // 2. Cancel upcoming trips that have not started
            DateTime today = DateTime.Today;
            var upcoming = db.Bookings
                .Where(b => b.BoatID == id
                         && b.Status == BookingStatus.Confirmed
                         && b.RideStartedAt == null
                         && b.TripDate >= today)
                .ToList();
            foreach (var booking in upcoming)
                booking.Status = BookingStatus.Cancelled;

            db.SaveChanges();

            // 3. Tell the affected customers and the owner
            foreach (var booking in upcoming)
            {
                NotificationHelper.Notify(db,
                    booking.CustomerID,
                    "Customer",
                    $"Your trip on '{boatName}' on {booking.TripDate.ToString("dd MMM yyyy")} has been cancelled because the boat was removed by TNPA. Please contact the boat owner about a refund.",
                    "/Customer/MyBookings");
            }

            NotificationHelper.Notify(db,
                boat.BoatOwnerID,
                "BoatOwner",
                $"Your boat '{boatName}' was removed by TNPA. Reason: {reason}" +
                (upcoming.Count > 0 ? $" {upcoming.Count} upcoming booking(s) were cancelled." : ""),
                "/BoatOwner/MyBoats");
            db.SaveChanges();

            TempData["SuccessMessage"] = $"Boat '{boatName}' has been removed. {upcoming.Count} upcoming booking(s) were cancelled and the customers notified.";
            return RedirectToAction("AllBoats");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}
