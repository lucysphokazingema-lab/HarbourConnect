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

            if (driver.AssignedBoatID != null)
            {
                ViewBag.UpcomingTrips = db.Bookings
                    .Count(b => b.BoatID == driver.AssignedBoatID
                             && b.Status == BookingStatus.Confirmed
                             && b.TripDate >= DateTime.Today);

                ViewBag.TotalTrips = db.Bookings
                    .Count(b => b.BoatID == driver.AssignedBoatID
                             && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed));

                // Find the next confirmed booking for this boat (today or future, not yet started)
                var nextBooking = db.Bookings
                    .Include("Customer")
                    .Where(b => b.BoatID == driver.AssignedBoatID
                             && b.Status == BookingStatus.Confirmed
                             && b.TripDate >= DateTime.Today
                             && b.RideStartedAt == null)
                    .OrderBy(b => b.TripDate)
                    .FirstOrDefault();

                // Find an active (started but not ended) ride
                var activeRide = db.Bookings
                    .Include("Customer")
                    .Where(b => b.BoatID == driver.AssignedBoatID
                             && b.RideStartedAt != null
                             && b.RideEndedAt == null)
                    .FirstOrDefault();

                ViewBag.NextBooking  = nextBooking;
                ViewBag.ActiveRide   = activeRide;
            }
            else
            {
                ViewBag.UpcomingTrips = 0;
                ViewBag.TotalTrips    = 0;
                ViewBag.NextBooking   = null;
                ViewBag.ActiveRide    = null;
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
                         && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed))
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

        // ─────────────────────────────────────────────
        // START RIDE
        // ─────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult StartRide(int bookingId)
        {
            if (Session["DriverID"] == null)
                return RedirectToAction("Login");

            int driverID = (int)Session["DriverID"];
            var driver = db.Drivers.FirstOrDefault(d => d.DriverID == driverID);

            if (driver == null)
                return RedirectToAction("Login");

            var booking = db.Bookings.FirstOrDefault(b => b.BookingID == bookingId
                                                       && b.BoatID == driver.AssignedBoatID
                                                       && b.Status == BookingStatus.Confirmed
                                                       && b.RideStartedAt == null);
            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found or ride already started.";
                return RedirectToAction("Dashboard");
            }

            booking.RideStartedAt = DateTime.Now;
            db.SaveChanges();

            TempData["SuccessMessage"] = "Ride started! Safe sailing.";
            return RedirectToAction("RideMap", new { bookingId = booking.BookingID });
        }

        // ─────────────────────────────────────────────
        // RIDE MAP
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult RideMap(int bookingId)
        {
            if (Session["DriverID"] == null)
                return RedirectToAction("Login");

            int driverID = (int)Session["DriverID"];
            var driver = db.Drivers
                .Include("AssignedBoat")
                .FirstOrDefault(d => d.DriverID == driverID);

            if (driver == null)
                return RedirectToAction("Login");

            var booking = db.Bookings
                .Include("Customer")
                .Include("Boat")
                .FirstOrDefault(b => b.BookingID == bookingId
                                  && b.BoatID == driver.AssignedBoatID
                                  && b.RideStartedAt != null
                                  && b.RideEndedAt == null);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Active ride not found.";
                return RedirectToAction("Dashboard");
            }

            return View(booking);
        }

        // ─────────────────────────────────────────────
        // END RIDE
        // ─────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EndRide(int bookingId,
            bool lifeJacketsReturned,
            bool boatSecured,
            bool noIncidents,
            bool passengersSafe,
            string endNotes)
        {
            if (Session["DriverID"] == null)
                return RedirectToAction("Login");

            int driverID = (int)Session["DriverID"];
            var driver = db.Drivers.FirstOrDefault(d => d.DriverID == driverID);

            if (driver == null)
                return RedirectToAction("Login");

            var booking = db.Bookings.FirstOrDefault(b => b.BookingID == bookingId
                                                       && b.BoatID == driver.AssignedBoatID
                                                       && b.RideStartedAt != null
                                                       && b.RideEndedAt == null);
            if (booking == null)
            {
                TempData["ErrorMessage"] = "Active ride not found.";
                return RedirectToAction("Dashboard");
            }

            if (!lifeJacketsReturned || !boatSecured || !passengersSafe)
            {
                TempData["ErrorMessage"] = "Please confirm all safety checks before ending the ride.";
                return RedirectToAction("RideMap", new { bookingId });
            }

            booking.RideEndedAt = DateTime.Now;
            booking.Status      = BookingStatus.Completed;
            db.SaveChanges();

            TempData["SuccessMessage"] = "Ride completed successfully. All checks passed.";
            return RedirectToAction("Dashboard");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}
