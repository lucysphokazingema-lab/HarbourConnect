using System;
using System.Linq;
using System.Web.Mvc;
using APDP.Models;

namespace APDP.Controllers
{
    public class DriverController : Controller
    {
        private HarbourConnectContext db = new HarbourConnectContext();

        // ─────────────────────────────────────────────
        // LOGIN
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult Login()
        {
            if (Session["DriverID"] != null)
                return RedirectToAction("Dashboard");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password)
        {
            var driver = db.Drivers
                .FirstOrDefault(d => d.Email == email && d.Password == password);

            if (driver != null)
            {
                if (driver.Status == DriverStatus.Suspended)
                {
                    ViewBag.ErrorMessage = "Your account has been suspended. Please contact your boat owner.";
                    return View();
                }

                Session["DriverID"]   = driver.DriverID;
                Session["DriverName"] = driver.FullName;
                Session["UserType"]   = "Driver";
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
            if (Session["DriverID"] == null)
                return RedirectToAction("Login");

            int driverID = (int)Session["DriverID"];
            var driver = db.Drivers
                .Include("AssignedBoat")
                .Include("BoatOwner")
                .FirstOrDefault(d => d.DriverID == driverID);

            if (driver == null)
            {
                Session.Clear();
                return RedirectToAction("Login");
            }

            // Count bookings for the assigned boat
            if (driver.AssignedBoatID != null)
            {
                ViewBag.UpcomingTrips = db.Bookings
                    .Count(b => b.BoatID == driver.AssignedBoatID
                             && b.Status == BookingStatus.Confirmed
                             && b.TripDate >= DateTime.Today);

                ViewBag.TotalTrips = db.Bookings
                    .Count(b => b.BoatID == driver.AssignedBoatID
                             && b.Status == BookingStatus.Confirmed);
            }
            else
            {
                ViewBag.UpcomingTrips = 0;
                ViewBag.TotalTrips    = 0;
            }

            return View(driver);
        }

        // ─────────────────────────────────────────────
        // MY TRIPS
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult MyTrips()
        {
            if (Session["DriverID"] == null)
                return RedirectToAction("Login");

            int driverID = (int)Session["DriverID"];
            var driver = db.Drivers.FirstOrDefault(d => d.DriverID == driverID);

            if (driver == null || driver.AssignedBoatID == null)
            {
                ViewBag.Message = "You are not currently assigned to a boat. Contact your boat owner.";
                return View(Enumerable.Empty<Booking>().ToList());
            }

            var trips = db.Bookings
                .Include("Customer")
                .Include("Boat")
                .Where(b => b.BoatID == driver.AssignedBoatID
                         && b.Status == BookingStatus.Confirmed)
                .OrderBy(b => b.TripDate)
                .ToList();

            return View(trips);
        }

        // ─────────────────────────────────────────────
        // PROFILE
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult Profile()
        {
            if (Session["DriverID"] == null)
                return RedirectToAction("Login");

            int driverID = (int)Session["DriverID"];
            var driver = db.Drivers
                .Include("AssignedBoat")
                .Include("BoatOwner")
                .FirstOrDefault(d => d.DriverID == driverID);

            if (driver == null)
            {
                Session.Clear();
                return RedirectToAction("Login");
            }

            return View(driver);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}
