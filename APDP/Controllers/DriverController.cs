using System;
using System.Linq;
using System.Web.Mvc;
using APDP.Helpers;
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
            var driver = db.Drivers.FirstOrDefault(d => d.Email == email);

            bool valid = driver != null && PasswordHasher.Verify(password, driver.PasswordHash);
            if (driver == null)
                PasswordHasher.VerifyDummy(password);

            if (valid)
            {
                if (driver.Status == DriverStatus.Suspended)
                {
                    ViewBag.ErrorMessage = "Your account has been suspended. Please contact your boat owner.";
                    return View();
                }

                // Clear any other portal sessions first
                Session.Remove("BoatOwnerID");  Session.Remove("BoatOwnerName");
                Session.Remove("CustomerID");   Session.Remove("CustomerName");
                Session.Remove("TnpaAdminID");  Session.Remove("TnpaAdminName");

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
                    .Include("Boat")
                    .Where(b => b.BoatID == driver.AssignedBoatID
                             && b.Status == BookingStatus.Confirmed
                             && b.TripDate >= DateTime.Today
                             && b.RideStartedAt == null)
                    .OrderBy(b => b.TripDate)
                    .ThenBy(b => b.TripStartMinutes)
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

                // ── Check for expired bookings — notify customer once ──────
                if (nextBooking != null && activeRide == null)
                {
                    DateTime bookedDT  = nextBooking.TripDate.Date.Add(nextBooking.TripStartTime);
                    DateTime latestDT  = bookedDT.AddHours(1);

                    if (DateTime.Now > latestDT)
                    {
                        // Only notify if no notification about this booking expiry exists yet
                        string expiryMsg = $"expired booking #{nextBooking.BookingID}";
                        bool alreadyNotified = db.Notifications.Any(n =>
                            n.UserID   == nextBooking.CustomerID &&
                            n.UserType == "Customer" &&
                            n.Message.Contains(expiryMsg));

                        if (!alreadyNotified)
                        {
                            NotificationHelper.Notify(db,
                                nextBooking.CustomerID,
                                "Customer",
                                $"Your trip on '{nextBooking.Boat.BoatName}' booked for " +
                                $"{bookedDT.ToString("HH:mm")} on {nextBooking.TripDate.ToString("dd MMM yyyy")} " +
                                $"was not started and the booking window has expired (expired booking #{nextBooking.BookingID}). " +
                                $"Please contact the boat owner for assistance.",
                                "/Customer/MyBookings");

                            // Also notify boat owner
                            if (driver.BoatOwnerID.HasValue)
                            {
                                NotificationHelper.Notify(db,
                                    driver.BoatOwnerID.Value,
                                    "BoatOwner",
                                    $"Booking #{nextBooking.BookingID} for '{nextBooking.Boat.BoatName}' " +
                                    $"on {nextBooking.TripDate.ToString("dd MMM yyyy")} at {bookedDT.ToString("HH:mm")} " +
                                    $"expired — the ride was never started.",
                                    "/BoatOwner/Bookings");
                            }

                            db.SaveChanges();
                        }
                    }
                }
            }
            else
            {
                ViewBag.UpcomingTrips = 0;
                ViewBag.TotalTrips    = 0;
                ViewBag.NextBooking   = null;
                ViewBag.ActiveRide    = null;
            }

            // ── Harbour Activity Feed ─────────────────────────────
            // Covers every boat at the harbour, so customer records are deliberately
            // not loaded here — drivers only see customer details for their own boat.
            // Rides currently in progress at the harbour
            var ridesInProgress = db.Bookings
                .Include("Boat")
                .Where(b => b.RideStartedAt != null && b.RideEndedAt == null)
                .OrderBy(b => b.RideStartedAt)
                .Take(10)
                .ToList();

            // Upcoming trips today at the harbour (all boats)
            DateTime todayStart = DateTime.Today;
            DateTime todayEnd   = todayStart.AddDays(1);
            var tripsToday = db.Bookings
                .Include("Boat")
                .Where(b => b.Status == BookingStatus.Confirmed
                         && b.TripDate >= todayStart
                         && b.TripDate < todayEnd
                         && b.RideStartedAt == null)
                .OrderBy(b => b.TripStartMinutes)
                .Take(10)
                .ToList();

            // Recently completed trips today
            var recentlyCompleted = db.Bookings
                .Include("Boat")
                .Where(b => b.Status == BookingStatus.Completed
                         && b.RideEndedAt != null
                         && b.RideEndedAt >= todayStart)
                .OrderByDescending(b => b.RideEndedAt)
                .Take(5)
                .ToList();

            ViewBag.RidesInProgress    = ridesInProgress;
            ViewBag.TripsToday         = tripsToday;
            ViewBag.RecentlyCompleted  = recentlyCompleted;
            ViewBag.HarbourLocation    = driver.AssignedBoat != null
                                            ? driver.AssignedBoat.HarbourLocation
                                            : "Durban Harbour";

            // ── All approved boats for the harbour map ────────────
            var allApprovedBoats = db.Boats
                .Include("BoatOwner")
                .Where(b => b.Status == BoatStatus.Active || b.Status == BoatStatus.Approved)
                .ToList();
            ViewBag.AllApprovedBoats = allApprovedBoats;

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
                .ThenBy(b => b.TripStartMinutes)
                .ToList();

            return View(trips);
        }

        // ─────────────────────────────────────────────
        // PROFILE
        // ─────────────────────────────────────────────

        [HttpGet]
        public new ActionResult Profile()
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
        public ActionResult StartRide(int? bookingId)
        {
            if (Session["DriverID"] == null)
                return RedirectToAction("Login");

            if (bookingId == null)
            {
                TempData["ErrorMessage"] = "No booking specified.";
                return RedirectToAction("Dashboard");
            }

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

            // ── Time validation: the ride can start from the booked time, up to 1 hour late ──
            DateTime bookedDateTime = booking.TripDate.Date.Add(booking.TripStartTime);
            DateTime earliestStart  = bookedDateTime;
            DateTime latestStart    = bookedDateTime.AddHours(1); // allow up to 1 hour late

            if (DateTime.Now < earliestStart)
            {
                int minutesUntil = (int)Math.Ceiling((bookedDateTime - DateTime.Now).TotalMinutes);
                TempData["ErrorMessage"] = $"It is too early to start this ride. " +
                    $"The trip is booked for {bookedDateTime.ToString("HH:mm")} on {bookedDateTime.ToString("dd MMM yyyy")}. " +
                    $"You can start it from that time. Please wait {minutesUntil} more minute{(minutesUntil == 1 ? "" : "s")}.";
                return RedirectToAction("Dashboard");
            }

            if (DateTime.Now > latestStart)
            {
                TempData["ErrorMessage"] = $"This ride can no longer be started. " +
                    $"The booked time was {bookedDateTime.ToString("HH:mm")} and it is now past the 1-hour grace window.";
                return RedirectToAction("Dashboard");
            }

            booking.RideStartedAt = DateTime.Now;
            db.SaveChanges();

            // Notify boat owner ride has started
            if (driver.BoatOwnerID.HasValue)
            {
                NotificationHelper.Notify(db,
                    driver.BoatOwnerID.Value,
                    "BoatOwner",
                    $"Ride started for booking #{booking.BookingID} on {booking.TripDate.ToString("dd MMM yyyy")}. Driver is on the water.",
                    "/BoatOwner/Bookings");
            }

            // Notify customer — ride has started, they need to board
            NotificationHelper.Notify(db,
                booking.CustomerID,
                "Customer",
                $"🚢 Your trip on '{booking.Boat.BoatName}' has started! " +
                $"Please make sure you are at {booking.Boat.HarbourLocation} and ready to board. " +
                $"Trip: {booking.TripDate.ToString("dd MMM yyyy")} at " +
                $"{booking.TripStartTime.Hours.ToString("D2")}:{booking.TripStartTime.Minutes.ToString("D2")}.",
                "/Customer/BookingConfirmation/" + booking.BookingID);

            // Notify driver their ride is confirmed started
            NotificationHelper.Notify(db,
                driver.DriverID,
                "Driver",
                $"Ride started for booking #{booking.BookingID}. Trip date: {booking.TripDate.ToString("dd MMM yyyy")}. Safe sailing!",
                "/Driver/RideMap?bookingId=" + booking.BookingID);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Ride started! Safe sailing.";
            return RedirectToAction("RideMap", new { bookingId = booking.BookingID });
        }

        // ─────────────────────────────────────────────
        // RIDE MAP
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult RideMap(int? bookingId)
        {
            if (Session["DriverID"] == null)
                return RedirectToAction("Login");

            if (bookingId == null)
            {
                TempData["ErrorMessage"] = "No booking specified.";
                return RedirectToAction("Dashboard");
            }

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
        public ActionResult EndRide(int? bookingId,
            bool lifeJacketsReturned,
            bool boatSecured,
            bool noIncidents,
            bool passengersSafe,
            string endNotes)
        {
            if (Session["DriverID"] == null)
                return RedirectToAction("Login");

            if (bookingId == null)
            {
                TempData["ErrorMessage"] = "No booking specified.";
                return RedirectToAction("Dashboard");
            }

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

            // Notify boat owner the trip is complete
            if (driver != null && driver.BoatOwnerID.HasValue)
            {
                NotificationHelper.Notify(db,
                    driver.BoatOwnerID.Value,
                    "BoatOwner",
                    $"Trip for booking #{booking.BookingID} on {booking.TripDate.ToString("dd MMM yyyy")} has been completed by driver.",
                    "/BoatOwner/Bookings");
            }

            // Notify customer trip is done and they can rate
            NotificationHelper.Notify(db,
                booking.CustomerID,
                "Customer",
                $"Your trip on {booking.TripDate.ToString("dd MMM yyyy")} is complete! Please rate your experience.",
                "/Customer/RateBoat?bookingId=" + booking.BookingID);

            // Notify driver trip is complete
            NotificationHelper.Notify(db,
                driver.DriverID,
                "Driver",
                $"Trip for booking #{booking.BookingID} on {booking.TripDate.ToString("dd MMM yyyy")} completed successfully.",
                "/Driver/MyTrips");

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
