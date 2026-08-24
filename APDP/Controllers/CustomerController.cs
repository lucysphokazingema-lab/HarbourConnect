using System;
using System.Linq;
using System.Web.Mvc;
using APDP.Models;
using APDP.Services;

namespace APDP.Controllers
{
    public class CustomerController : Controller
    {
        private HarbourConnectContext db = new HarbourConnectContext();
        private WeatherService weatherService = new WeatherService();

        // ─────────────────────────────────────────────
        // REGISTER
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(Customer model)
        {
            if (ModelState.IsValid)
            {
                if (db.Customers.Any(x => x.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "This email address is already registered.");
                    return View(model);
                }

                model.DateRegistered = DateTime.Now;
                db.Customers.Add(model);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Account created successfully! Please log in.";
                return RedirectToAction("Login");
            }

            return View(model);
        }

        // ─────────────────────────────────────────────
        // LOGIN
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult Login()
        {
            if (Session["CustomerID"] != null)
                return RedirectToAction("Dashboard");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password)
        {
            var customer = db.Customers
                .FirstOrDefault(x => x.Email == email && x.Password == password);

            if (customer != null)
            {
                Session["CustomerID"]   = customer.CustomerID;
                Session["CustomerName"] = customer.FullName;
                Session["UserType"]     = "Customer";
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
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            int customerID = (int)Session["CustomerID"];

            // Fetch bookings once, count in memory — avoids 3 separate DB round-trips
            var myBookings = db.Bookings
                .Where(b => b.CustomerID == customerID)
                .Select(b => new { b.Status, b.IsRated })
                .ToList();

            ViewBag.TotalBookings  = myBookings.Count;
            ViewBag.AvailableBoats = db.Boats.Count(b => b.Status == BoatStatus.Approved);
            ViewBag.UnratedTrips   = myBookings.Count(b => b.Status == BookingStatus.Completed && !b.IsRated);

            return View();
        }

        // ─────────────────────────────────────────────
        // BROWSE BOATS
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult Boats(string location, string boatType)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            var boats = db.Boats
                .Include("BoatOwner")
                .Where(b => b.Status == BoatStatus.Approved);

            if (!string.IsNullOrWhiteSpace(location))
                boats = boats.Where(b => b.HarbourLocation.Contains(location));

            if (!string.IsNullOrWhiteSpace(boatType))
                boats = boats.Where(b => b.BoatType == boatType);

            ViewBag.SelectedLocation = location;
            ViewBag.SelectedType     = boatType;

            return View(boats.ToList());
        }

        // ─────────────────────────────────────────────
        // BOAT DETAILS
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult BoatDetails(int id)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            // Load boat + owner only — no need to include all Ratings here
            var boat = db.Boats
                .Include("BoatOwner")
                .FirstOrDefault(b => b.BoatID == id && b.Status == BoatStatus.Approved);

            if (boat == null)
            {
                TempData["ErrorMessage"] = "Boat not found.";
                return RedirectToAction("Boats");
            }

            // Load up to 5 recent ratings separately — lighter than a full join
            var recentRatings = db.BoatRatings
                .Include("Customer")
                .Where(r => r.BoatID == id)
                .OrderByDescending(r => r.DateRated)
                .Take(5)
                .ToList();

            ViewBag.RecentRatings = recentRatings;

            return View(boat);
        }

        // ─────────────────────────────────────────────
        // BOOK A BOAT
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult BookBoat(int id)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            var boat = db.Boats
                .Include("BoatOwner")
                .FirstOrDefault(b => b.BoatID == id && b.Status == BoatStatus.Approved);

            if (boat == null)
            {
                TempData["ErrorMessage"] = "Boat not found or not available for booking.";
                return RedirectToAction("Boats");
            }

            var weather = weatherService.GetWeather(boat.HarbourLocation);

            ViewBag.Boat    = boat;
            ViewBag.Weather = weather;

            var booking = new Booking
            {
                BoatID     = id,
                TripDate   = DateTime.Now.AddDays(1),
                TotalPrice = boat.PricePerTrip
            };

            return View(booking);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BookBoat(Booking model)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            var boat = db.Boats.Find(model.BoatID);

            if (boat == null || boat.Status != BoatStatus.Approved)
            {
                TempData["ErrorMessage"] = "This boat is no longer available.";
                return RedirectToAction("Boats");
            }

            if (model.TripDate < DateTime.Today.AddDays(1))
                ModelState.AddModelError("TripDate", "Trip date must be at least one day in the future.");

            if (model.NumberOfPassengers > boat.MaxPassengers)
                ModelState.AddModelError("NumberOfPassengers",
                    $"This boat allows a maximum of {boat.MaxPassengers} passengers.");

            // Check if this boat already has a confirmed booking on the same date
            bool dateAlreadyBooked = db.Bookings.Any(b =>
                b.BoatID == model.BoatID &&
                b.Status == BookingStatus.Confirmed &&
                b.TripDate == model.TripDate);

            if (dateAlreadyBooked)
                ModelState.AddModelError("TripDate",
                    $"This boat is already fully booked on {model.TripDate.ToString("dd MMMM yyyy")}. Please choose a different date.");

            if (ModelState.IsValid)
            {
                model.CustomerID  = (int)Session["CustomerID"];
                model.Status      = BookingStatus.Confirmed;
                model.BookingDate = DateTime.Now;
                model.TotalPrice  = boat.PricePerTrip * model.NumberOfPassengers;
                model.IsRated     = false;

                db.Bookings.Add(model);
                db.SaveChanges();

                // Redirect to confirmation slip instead of MyBookings
                return RedirectToAction("BookingConfirmation", new { id = model.BookingID });
            }

            ViewBag.Boat = boat;
            return View(model);
        }

        // ─────────────────────────────────────────────
        // BOOKING CONFIRMATION SLIP (with QR code)
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult BookingConfirmation(int id)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            int customerID = (int)Session["CustomerID"];
            var booking = db.Bookings
                .Include("Boat")
                .Include("Boat.BoatOwner")
                .Include("Customer")
                .FirstOrDefault(b => b.BookingID == id && b.CustomerID == customerID);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found.";
                return RedirectToAction("MyBookings");
            }

            return View(booking);
        }

        // ─────────────────────────────────────────────
        // MY BOOKINGS
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult MyBookings()
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            int customerID = (int)Session["CustomerID"];
            var bookings = db.Bookings
                .Include("Boat")
                .Include("Boat.BoatOwner")
                .Where(b => b.CustomerID == customerID)
                .OrderByDescending(b => b.BookingDate)
                .ToList();

            return View(bookings);
        }

        // ─────────────────────────────────────────────
        // CANCEL BOOKING
        // ─────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelBooking(int id)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            int customerID = (int)Session["CustomerID"];
            var booking = db.Bookings
                .FirstOrDefault(b => b.BookingID == id && b.CustomerID == customerID);

            if (booking != null && booking.Status == BookingStatus.Confirmed)
            {
                if (booking.TripDate > DateTime.Now.AddDays(1))
                {
                    booking.Status = BookingStatus.Cancelled;
                    db.SaveChanges();
                    TempData["SuccessMessage"] = "Booking cancelled successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Cannot cancel a booking within 24 hours of the trip.";
                }
            }

            return RedirectToAction("MyBookings");
        }

        // ─────────────────────────────────────────────
        // RATE BOAT (GET — show form)
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult RateBoat(int bookingId)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            int customerID = (int)Session["CustomerID"];
            var booking = db.Bookings
                .Include("Boat")
                .FirstOrDefault(b => b.BookingID == bookingId
                                  && b.CustomerID == customerID
                                  && b.Status == BookingStatus.Completed
                                  && !b.IsRated);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "This booking is not available for rating.";
                return RedirectToAction("MyBookings");
            }

            ViewBag.Booking = booking;
            return View(new BoatRating
            {
                BoatID     = booking.BoatID,
                BookingID  = booking.BookingID,
                CustomerID = customerID
            });
        }

        // ─────────────────────────────────────────────
        // RATE BOAT (POST — save rating)
        // ─────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RateBoat(BoatRating model)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            int customerID = (int)Session["CustomerID"];

            // Verify the booking belongs to this customer and is completed/unrated
            var booking = db.Bookings.FirstOrDefault(b => b.BookingID == model.BookingID
                                                       && b.CustomerID == customerID
                                                       && b.Status == BookingStatus.Completed
                                                       && !b.IsRated);
            if (booking == null)
            {
                TempData["ErrorMessage"] = "This booking is not available for rating.";
                return RedirectToAction("MyBookings");
            }

            if (ModelState.IsValid)
            {
                model.CustomerID = customerID;
                model.DateRated  = DateTime.Now;

                db.BoatRatings.Add(model);

                // Mark booking as rated
                booking.IsRated = true;

                // Recalculate boat's average rating
                var boat = db.Boats.Find(model.BoatID);
                if (boat != null)
                {
                    var allRatings = db.BoatRatings.Where(r => r.BoatID == model.BoatID).ToList();
                    int totalStars = allRatings.Sum(r => r.Stars) + model.Stars;
                    int totalCount = allRatings.Count + 1;
                    boat.AverageRating = Math.Round((double)totalStars / totalCount, 1);
                    boat.TotalRatings  = totalCount;
                }

                db.SaveChanges();

                TempData["SuccessMessage"] = "Thank you for your rating! Your feedback helps other customers.";
                return RedirectToAction("MyBookings");
            }

            // Re-load booking info for the view on validation failure
            var bookingInfo = db.Bookings.Include("Boat").FirstOrDefault(b => b.BookingID == model.BookingID);
            ViewBag.Booking = bookingInfo;
            return View(model);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}
