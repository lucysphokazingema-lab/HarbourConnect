using System.Linq;
using System.Web.Mvc;
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
            var admin = db.TnpaAdmins
                .FirstOrDefault(x => x.Email == email && x.Password == password);

            if (admin != null)
            {
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
            ViewBag.ApprovedCount = db.Boats.Count(b => b.Status == BoatStatus.Approved);
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
        public ActionResult BoatDetails(int id)
        {
            if (Session["TnpaAdminID"] == null)
                return RedirectToAction("Login");

            var boat = db.Boats
                .Include("BoatOwner")
                .FirstOrDefault(b => b.BoatID == id);

            if (boat == null)
            {
                TempData["ErrorMessage"] = "Boat not found.";
                return RedirectToAction("PendingBoats");
            }

            return View(boat);
        }

        // ─────────────────────────────────────────────
        // APPROVE
        // ─────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(int id)
        {
            if (Session["TnpaAdminID"] == null)
                return RedirectToAction("Login");

            var boat = db.Boats.Find(id);
            if (boat != null)
            {
                boat.Status          = BoatStatus.Approved;
                boat.RejectionReason = null;
                db.SaveChanges();
                TempData["SuccessMessage"] = $"Boat '{boat.BoatName}' has been approved successfully.";
            }

            return RedirectToAction("PendingBoats");
        }

        // ─────────────────────────────────────────────
        // REJECT
        // ─────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reject(int id, string rejectionReason)
        {
            if (Session["TnpaAdminID"] == null)
                return RedirectToAction("Login");

            var boat = db.Boats.Find(id);
            if (boat != null)
            {
                boat.Status          = BoatStatus.Rejected;
                boat.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason)
                    ? "Rejected by TNPA Admin."
                    : rejectionReason;
                db.SaveChanges();
                TempData["SuccessMessage"] = $"Boat '{boat.BoatName}' has been rejected.";
            }

            return RedirectToAction("PendingBoats");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}
