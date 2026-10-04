using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using APDP.Helpers;
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
                model.PasswordHash   = PasswordHasher.Hash(model.Password);
                db.Customers.Add(model);
                db.SaveChanges();

                Session.Clear();
                Session.Abandon();

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
            var customer = db.Customers.FirstOrDefault(x => x.Email == email);

            bool valid = customer != null && PasswordHasher.Verify(password, customer.PasswordHash);
            if (customer == null)
                PasswordHasher.VerifyDummy(password);

            if (valid)
            {
                // Clear any other portal sessions first
                Session.Remove("BoatOwnerID");  Session.Remove("BoatOwnerName");
                Session.Remove("DriverID");     Session.Remove("DriverName");
                Session.Remove("TnpaAdminID");  Session.Remove("TnpaAdminName");

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

            var myBookings = db.Bookings
                .Where(b => b.CustomerID == customerID)
                .Select(b => new { b.Status, b.IsRated })
                .ToList();

            ViewBag.TotalBookings  = myBookings.Count;
            ViewBag.AvailableBoats = db.Boats.Count(b => b.Status == BoatStatus.Active || b.Status == BoatStatus.Approved);
            ViewBag.UnratedTrips   = myBookings.Count(b => b.Status == BookingStatus.Completed && !b.IsRated);

            return View();
        }

        // ─────────────────────────────────────────────
        // BROWSE BOATS
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult Boats(string boatType, string searchName, string sortPrice)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            var boats = db.Boats
                .Include("BoatOwner")
                .Where(b => b.Status == BoatStatus.Active || b.Status == BoatStatus.Approved || b.Status == BoatStatus.Inactive);

            if (!string.IsNullOrWhiteSpace(boatType))
                boats = boats.Where(b => b.BoatType == boatType);

            if (!string.IsNullOrWhiteSpace(searchName))
                boats = boats.Where(b => b.BoatName.Contains(searchName));

            if (sortPrice == "asc")
                boats = boats.OrderBy(b => b.PriceAdult);
            else if (sortPrice == "desc")
                boats = boats.OrderByDescending(b => b.PriceAdult);

            ViewBag.SelectedType  = boatType;
            ViewBag.SearchName    = searchName;
            ViewBag.SortPrice     = sortPrice;

            return View(boats.ToList());
        }

        // ─────────────────────────────────────────────
        // BOAT DETAILS
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult BoatDetails(int? id)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            if (id == null)
            {
                TempData["ErrorMessage"] = "No boat specified.";
                return RedirectToAction("Boats");
            }

            var boat = db.Boats
                .Include("BoatOwner")
                .Include("Extras")
                .FirstOrDefault(b => b.BoatID == id && (b.Status == BoatStatus.Active || b.Status == BoatStatus.Approved));

            if (boat == null)
            {
                TempData["ErrorMessage"] = "Boat not found.";
                return RedirectToAction("Boats");
            }

            var recentRatings = db.BoatRatings
                .Include("Customer")
                .Where(r => r.BoatID == id)
                .OrderByDescending(r => r.DateRated)
                .Take(5)
                .ToList();

            ViewBag.RecentRatings = recentRatings;
            ViewBag.Week          = BoatAvailability.WeekFor(db, boat);

            return View(boat);
        }

        // ─────────────────────────────────────────────
        // AVAILABLE START TIMES (AJAX - booking page)
        // ─────────────────────────────────────────────

        [HttpGet]
        public JsonResult GetAvailableTimes(int boatId, string date, int hours)
        {
            if (Session["CustomerID"] == null)
                return Json(new string[0], JsonRequestBehavior.AllowGet);

            DateTime day;
            if (!DateTime.TryParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                                        System.Globalization.DateTimeStyles.None, out day))
                return Json(new string[0], JsonRequestBehavior.AllowGet);

            var boat = db.Boats.FirstOrDefault(b => b.BoatID == boatId &&
                                                    (b.Status == BoatStatus.Active || b.Status == BoatStatus.Approved));
            if (boat == null)
                return Json(new string[0], JsonRequestBehavior.AllowGet);

            var week  = BoatAvailability.WeekFor(db, boat);
            var times = BoatAvailability.FreeStartTimes(db, boat, week, day, hours)
                                        .Select(BoatAvailability.FormatTime)
                                        .ToArray();

            return Json(times, JsonRequestBehavior.AllowGet);
        }

        // ─────────────────────────────────────────────
        // BOOK A BOAT
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult BookBoat(int? id)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            if (id == null)
            {
                TempData["ErrorMessage"] = "No boat specified.";
                return RedirectToAction("Boats");
            }

            var boat = db.Boats
                .Include("BoatOwner")
                .Include("Extras")
                .FirstOrDefault(b => b.BoatID == id && (b.Status == BoatStatus.Active || b.Status == BoatStatus.Approved));

            if (boat == null)
            {
                TempData["ErrorMessage"] = "Boat not found or not available for booking.";
                return RedirectToAction("Boats");
            }

            var booking = new Booking
            {
                BoatID            = boat.BoatID,
                TripDurationHours = BoatAvailability.MinHours(boat),
                TotalPrice        = boat.PriceAdult
            };

            PrepareBookingPage(boat);
            return View(booking);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BookBoat(Booking model, string selectedTime)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            var boat = db.Boats
                .Include("BoatOwner")
                .Include("Extras")
                .FirstOrDefault(b => b.BoatID == model.BoatID);

            if (boat == null || (boat.Status != BoatStatus.Approved && boat.Status != BoatStatus.Active))
            {
                TempData["ErrorMessage"] = "This boat is no longer available.";
                return RedirectToAction("Boats");
            }

            var week = BoatAvailability.WeekFor(db, boat);
            model.TripDate = model.TripDate.Date;

            if (model.TripDate < DateTime.Today)
                ModelState.AddModelError("TripDate", "Trip date cannot be in the past.");

            if (model.NumberOfPassengers < 1 && model.ChildPassengers < 1)
                ModelState.AddModelError("NumberOfPassengers", "At least 1 passenger (adult or child) is required.");

            int minHours = BoatAvailability.MinHours(boat);
            int maxHours = BoatAvailability.MaxHours(boat, week);
            if (model.TripDurationHours < minHours || model.TripDurationHours > maxHours)
                ModelState.AddModelError("",
                    $"Trips on this boat are {minHours} to {maxHours} hours long. Please choose a duration in that range.");

            int totalPassengers = model.NumberOfPassengers + model.ChildPassengers;
            if (totalPassengers > boat.MaxPassengers)
                ModelState.AddModelError("NumberOfPassengers",
                    $"This boat allows a maximum of {boat.MaxPassengers} passengers total.");

            int startMinutes;
            if (!BoatAvailability.TryParseTime(selectedTime, out startMinutes))
                ModelState.AddModelError("", "Please choose a start time.");
            else
                model.TripStartMinutes = startMinutes;

            if (ModelState.IsValid)
            {
                int adults   = model.NumberOfPassengers;
                int children = model.ChildPassengers;
                int hours    = model.TripDurationHours;
                model.CustomerID       = (int)Session["CustomerID"];
                model.Status           = BookingStatus.Pending;   // confirmed once paid
                model.BookingDate      = DateTime.Now;
                model.IsRated          = false;
                model.RideStartedAt    = null;
                model.RideEndedAt      = null;
                model.PaidAt           = null;
                model.PaymentReference = null;

                // Hourly pricing: passengers × rate/hr × hours booked
                decimal basePrice = (boat.PriceAdult * adults * hours) + (boat.PriceChild * children * hours);

                var selectedExtraIds = (Request.Form.GetValues("selectedExtras") ?? new string[0])
                    .Select(s => { int x; return int.TryParse(s, out x) ? x : 0; })
                    .Where(x => x > 0)
                    .ToList();

                decimal extrasTotal = 0m;
                var extraNames = new List<string>();
                if (selectedExtraIds.Any())
                {
                    var extrasChosen = db.BoatExtras
                        .Where(e => e.BoatID == model.BoatID && selectedExtraIds.Contains(e.ExtraID))
                        .ToList();
                    extrasTotal = extrasChosen.Sum(e => e.Price);
                    extraNames  = extrasChosen.Select(e => e.Name + " (R" + e.Price.ToString("N2") + ")").ToList();
                }

                model.TotalPrice     = basePrice + extrasTotal;
                model.ExtrasTotal    = extrasTotal;
                model.SelectedExtras = extraNames.Any() ? string.Join(", ", extraNames) : null;

                // Re-check the slot and hold it under a per-boat lock so two customers
                // submitting at the same moment cannot both get it.
                bool slotFree;
                using (var tx = db.Database.BeginTransaction())
                {
                    LockBoatSchedule(model.BoatID);
                    slotFree = BoatAvailability
                        .FreeStartTimes(db, boat, week, model.TripDate, model.TripDurationHours)
                        .Contains(model.TripStartMinutes);
                    if (slotFree)
                    {
                        db.Bookings.Add(model);
                        db.SaveChanges();
                    }
                    tx.Commit();
                }

                if (slotFree)
                    return RedirectToAction("PayBooking", new { id = model.BookingID });

                ModelState.AddModelError("",
                    "Sorry, that start time is no longer available. Please choose another time.");
            }

            PrepareBookingPage(boat);
            return View(model);
        }

        private void PrepareBookingPage(Boat boat)
        {
            var week = BoatAvailability.WeekFor(db, boat);
            ViewBag.Boat     = boat;
            ViewBag.Extras   = boat.Extras.ToList();
            ViewBag.Weather  = weatherService.GetWeather(boat.HarbourLocation);
            ViewBag.Week     = week;
            ViewBag.MinHours = BoatAvailability.MinHours(boat);
            ViewBag.MaxHours = BoatAvailability.MaxHours(boat, week);
            ViewBag.Calendar = BoatAvailability.Calendar(db, boat, week, DateTime.Today, 28);
        }

        // ─────────────────────────────────────────────
        // PAY FOR A BOOKING (demo payment — no card details are collected)
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult PayBooking(int? id)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            int customerID = (int)Session["CustomerID"];
            var booking = db.Bookings
                .Include("Boat")
                .FirstOrDefault(b => b.BookingID == id && b.CustomerID == customerID);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found.";
                return RedirectToAction("MyBookings");
            }

            if (booking.Status != BookingStatus.Pending)
                return RedirectToAction("BookingConfirmation", new { id = booking.BookingID });

            ViewBag.HoldExpires = booking.BookingDate + BoatAvailability.PaymentHold;
            return View(booking);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("PayBooking")]
        public ActionResult PayBookingConfirm(int? id)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            int customerID = (int)Session["CustomerID"];
            var booking = db.Bookings
                .Include("Boat")
                .FirstOrDefault(b => b.BookingID == id && b.CustomerID == customerID);

            if (booking == null || booking.Status != BookingStatus.Pending)
            {
                TempData["ErrorMessage"] = "This booking is not awaiting payment.";
                return RedirectToAction("MyBookings");
            }

            var boat = booking.Boat;
            bool canConfirm;
            using (var tx = db.Database.BeginTransaction())
            {
                LockBoatSchedule(booking.BoatID);

                // Within the hold the slot is ours; after it, only if nobody else has taken it.
                canConfirm = booking.BookingDate > DateTime.Now - BoatAvailability.PaymentHold ||
                             BoatAvailability.FreeStartTimes(db, boat, BoatAvailability.WeekFor(db, boat),
                                                             booking.TripDate, booking.TripDurationHours,
                                                             ignoreBookingId: booking.BookingID)
                                             .Contains(booking.TripStartMinutes);

                if (canConfirm)
                {
                    // DEMO PAYMENT: replace with a real payment provider (e.g. PayFast hosted checkout)
                    // before going live. No card data is ever collected by this app.
                    booking.Status           = BookingStatus.Confirmed;
                    booking.PaidAt           = DateTime.Now;
                    booking.PaymentReference = "DEMO-" + booking.BookingID.ToString("D6") + "-" + DateTime.Now.ToString("HHmmss");
                }
                else
                {
                    booking.Status = BookingStatus.Cancelled;
                }
                db.SaveChanges();
                tx.Commit();
            }

            if (!canConfirm)
            {
                TempData["ErrorMessage"] = "Your 15-minute payment window expired and the time slot was taken. Please book a new time.";
                return RedirectToAction("BookBoat", new { id = booking.BoatID });
            }

            string when = booking.TripDate.ToString("dd MMM yyyy") + " at " + BoatAvailability.FormatTime(booking.TripStartMinutes);

            NotificationHelper.Notify(db,
                booking.CustomerID,
                "Customer",
                $"Booking confirmed! Your trip on '{boat.BoatName}' on {when} is paid (R {booking.TotalPrice:N2}, ref {booking.PaymentReference}).",
                "/Customer/BookingConfirmation/" + booking.BookingID);

            NotificationHelper.Notify(db,
                boat.BoatOwnerID,
                "BoatOwner",
                $"New paid booking for '{boat.BoatName}' on {when} ({booking.NumberOfPassengers + booking.ChildPassengers} passengers).",
                "/BoatOwner/Bookings");

            // Let the drivers assigned to this boat know about the new trip
            foreach (var driver in db.Drivers.Where(d => d.AssignedBoatID == boat.BoatID && d.Status == DriverStatus.Active).ToList())
            {
                NotificationHelper.Notify(db,
                    driver.DriverID,
                    "Driver",
                    $"New trip booked on '{boat.BoatName}' for {when}.",
                    "/Driver/MyTrips");
            }
            db.SaveChanges();

            TempData["SuccessMessage"] = "Payment received — your booking is confirmed.";
            return RedirectToAction("BookingConfirmation", new { id = booking.BookingID });
        }

        // Serialises bookings per boat for the rest of the current transaction
        // (SQL Server application lock; released automatically on commit/rollback).
        private void LockBoatSchedule(int boatId)
        {
            int result = db.Database.SqlQuery<int>(
                "DECLARE @r int; " +
                "EXEC @r = sp_getapplock @Resource = @p0, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; " +
                "SELECT @r;",
                "HarbourConnect.BoatSchedule." + boatId).Single();

            if (result < 0)
                throw new InvalidOperationException("Could not lock the boat schedule (sp_getapplock returned " + result + ").");
        }

        // ─────────────────────────────────────────────
        // BOOKING CONFIRMATION SLIP
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult BookingConfirmation(int? id)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            if (id == null)
            {
                TempData["ErrorMessage"] = "No booking specified.";
                return RedirectToAction("MyBookings");
            }

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

            if (booking.Status == BookingStatus.Pending)
                return RedirectToAction("PayBooking", new { id = booking.BookingID });

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

            // Unpaid bookings for trips that have already passed can never be paid — close them off.
            DateTime today = DateTime.Today;
            var stale = db.Bookings
                .Where(b => b.CustomerID == customerID && b.Status == BookingStatus.Pending && b.TripDate < today)
                .ToList();
            if (stale.Any())
            {
                foreach (var s in stale) s.Status = BookingStatus.Cancelled;
                db.SaveChanges();
            }

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
        public ActionResult CancelBooking(int? id)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            if (id == null)
                return RedirectToAction("MyBookings");

            int customerID = (int)Session["CustomerID"];
            var booking = db.Bookings
                .FirstOrDefault(b => b.BookingID == id && b.CustomerID == customerID);

            if (booking != null && booking.Status == BookingStatus.Pending)
            {
                // Not paid yet — simply release the slot.
                booking.Status = BookingStatus.Cancelled;
                db.SaveChanges();
                TempData["SuccessMessage"] = "Unpaid booking cancelled.";
            }
            else if (booking != null && booking.Status == BookingStatus.Confirmed)
            {
                if (booking.TripDate.AddMinutes(booking.TripStartMinutes) > DateTime.Now.AddDays(1))
                {
                    booking.Status = BookingStatus.Cancelled;
                    db.SaveChanges();

                    var cancelledBoat = db.Boats.Find(booking.BoatID);
                    if (cancelledBoat != null)
                    {
                        NotificationHelper.Notify(db,
                            cancelledBoat.BoatOwnerID,
                            "BoatOwner",
                            $"A customer cancelled their booking for '{cancelledBoat.BoatName}' on {booking.TripDate.ToString("dd MMM yyyy")}.",
                            "/BoatOwner/Bookings");
                        db.SaveChanges();
                    }

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
        // RATE BOAT (GET)
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult RateBoat(int? bookingId)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            if (bookingId == null)
            {
                TempData["ErrorMessage"] = "No booking specified.";
                return RedirectToAction("MyBookings");
            }

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
        // RATE BOAT (POST)
        // ─────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RateBoat(BoatRating model)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            int customerID = (int)Session["CustomerID"];

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
                // Never trust the posted BoatID — the rating is for the boat on this booking.
                model.BoatID     = booking.BoatID;
                model.CustomerID = customerID;
                model.DateRated  = DateTime.Now;

                db.BoatRatings.Add(model);
                booking.IsRated = true;

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
