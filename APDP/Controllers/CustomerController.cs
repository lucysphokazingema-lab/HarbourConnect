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
            ViewBag.TotalBookings = db.Bookings.Count(b => b.CustomerID == customerID);
            ViewBag.AvailableBoats = db.Boats.Count(b => b.Status == BoatStatus.Approved);

            return View();
        }

        // ─────────────────────────────────────────────
        // BROWSE BOATS (approved only)
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

            var boat = db.Boats
                .Include("BoatOwner")
                .FirstOrDefault(b => b.BoatID == id && b.Status == BoatStatus.Approved);

            if (boat == null)
            {
                TempData["ErrorMessage"] = "Boat not found.";
                return RedirectToAction("Boats");
            }

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

            // Fetch live weather for the harbour location
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
            {
                ModelState.AddModelError("TripDate", "Trip date must be at least one day in the future.");
            }

            if (model.NumberOfPassengers > boat.MaxPassengers)
            {
                ModelState.AddModelError("NumberOfPassengers",
                    $"This boat allows a maximum of {boat.MaxPassengers} passengers.");
            }

            if (ModelState.IsValid)
            {
                model.CustomerID   = (int)Session["CustomerID"];
                model.Status       = BookingStatus.Confirmed;
                model.BookingDate  = DateTime.Now;
                model.TotalPrice   = boat.PricePerTrip * model.NumberOfPassengers;

                db.Bookings.Add(model);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Booking confirmed! Have a great trip.";
                return RedirectToAction("MyBookings");
            }

            ViewBag.Boat = boat;
            return View(model);
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
                    TempData["ErrorMessage"] = "Bookings cannot be cancelled within 24 hours of the trip.";
                }
            }

            return RedirectToAction("MyBookings");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}
