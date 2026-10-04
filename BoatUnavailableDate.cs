using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web.Mvc;
using APDP.Models;

namespace APDP.Controllers
{
    public class CustomerController : Controller
    {
        // Other actions...

        [HttpGet]
        public ActionResult BookBoat(int? id, string date)
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

            var weather = weatherService.GetWeather(boat.HarbourLocation);

            ViewBag.Boat    = boat;
            ViewBag.Extras  = boat.Extras.ToList();
            ViewBag.Weather = weather;

            // determine selected date (allow ?date=yyyy-MM-dd), default next day
            DateTime selectedDate = DateTime.Today.AddDays(1);
            if (!string.IsNullOrWhiteSpace(date))
            {
                DateTime parsed;
                if (DateTime.TryParse(date, out parsed))
                    selectedDate = parsed.Date;
            }

            // Generate available hourly slots between 08:00 and 17:00 given boat duration
            TimeSpan harbourOpen = TimeSpan.FromHours(8);   // 08:00
            TimeSpan harbourClose = TimeSpan.FromHours(17); // 17:00
            int duration = boat.DefaultTripDurationMinutes > 0 ? boat.DefaultTripDurationMinutes : 60;
            List<string> availableSlots = new List<string>();

            // If the selected date is marked unavailable, return empty slots
            bool isUnavailable = db.BoatUnavailableDates.Any(u => u.BoatID == boat.BoatID && u.Date == selectedDate.Date);
            if (!isUnavailable)
            {
                for (TimeSpan slot = harbourOpen; slot + TimeSpan.FromMinutes(duration) <= harbourClose; slot = slot.Add(TimeSpan.FromHours(1)))
                {
                    // Check overlapping confirmed bookings
                    DateTime slotStart = selectedDate.Date.Add(slot);
                    DateTime slotEnd   = slotStart.AddMinutes(duration);

                    bool overlap = db.Bookings.Any(b =>
                        b.BoatID == boat.BoatID &&
                        b.Status == BookingStatus.Confirmed &&
                        DbFunctions.TruncateTime(b.TripDate) == selectedDate.Date &&
                        (
                            // If existing booking has TripStartTime 00:00 and previously stored as date-only, treat as full-booked on that date
                            b.TripStartTime == TimeSpan.Zero ||
                            // compute existing booking start/end
                            DbFunctions.AddMilliseconds(b.TripDate, 0) != null && (
                                // overlapping check: existingStart < newEnd && existingEnd > newStart
                                ((DateTime)(b.TripDate + b.TripStartTime)) < slotEnd &&
                                ((DateTime)(b.TripDate + b.TripStartTime)) + TimeSpan.FromMinutes(duration) > slotStart
                            )
                        )
                    );

                    if (!overlap)
                        availableSlots.Add(slot.ToString(@"hh\:mm"));
                }
            }

            ViewBag.AvailableSlots = availableSlots;
            ViewBag.SelectedDate = selectedDate.ToString("yyyy-MM-dd");
            ViewBag.DefaultDuration = duration;

            var booking = new Booking
            {
                BoatID     = boat.BoatID,
                TripDate   = selectedDate,
                TripStartTime = availableSlots.Any() ? TimeSpan.Parse(availableSlots.First()) : TimeSpan.FromHours(8),
                TotalPrice = boat.PriceAdult
            };

            return View(booking);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BookBoat(Booking model)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("Login");

            // Always load boat with BoatOwner + Extras so the view summary can render on failure
            var boat = db.Boats
                .Include("BoatOwner")
                .Include("Extras")
                .FirstOrDefault(b => b.BoatID == model.BoatID);

            if (boat == null || (boat.Status != BoatStatus.Approved && boat.Status != BoatStatus.Active))
            {
                TempData["ErrorMessage"] = "This boat is no longer available.";
                return RedirectToAction("Boats");
            }

            // Basic date/time checks
            if (model.TripDate.Date < DateTime.Today.AddDays(1))
                ModelState.AddModelError("TripDate", "Trip date must be at least one day in the future.");

            // Time validation
            TimeSpan harbourOpen = TimeSpan.FromHours(8);
            TimeSpan harbourClose = TimeSpan.FromHours(17);
            int duration = boat.DefaultTripDurationMinutes > 0 ? boat.DefaultTripDurationMinutes : 60;

            if (model.TripStartTime == null || model.TripStartTime == TimeSpan.Zero && model.TripStartTime != TimeSpan.Zero)
            {
                ModelState.AddModelError("TripStartTime", "Please select a valid start time.");
            }
            else
            {
                // ensure start+duration within operating hours
                if (model.TripStartTime < harbourOpen || model.TripStartTime.Add(TimeSpan.FromMinutes(duration)) > harbourClose)
                    ModelState.AddModelError("TripStartTime", $"Trips must start between {harbourOpen:hh\\:mm} and {(harbourClose - TimeSpan.FromMinutes(duration)):hh\\:mm}.");
            }

            if (model.NumberOfPassengers < 1 && model.ChildPassengers < 1)
                ModelState.AddModelError("NumberOfPassengers", "At least 1 passenger (adult or child) is required.");

            int totalPassengers = model.NumberOfPassengers + model.ChildPassengers;
            if (totalPassengers > boat.MaxPassengers)
                ModelState.AddModelError("NumberOfPassengers",
                    $"This boat allows a maximum of {boat.MaxPassengers} passengers total.");

            // Check if boat unavailable for that date
            bool isUnavailable = db.BoatUnavailableDates.Any(u => u.BoatID == boat.BoatID && u.Date == model.TripDate.Date);
            if (isUnavailable)
                ModelState.AddModelError("", "This boat is not available on the selected date.");

            // Overlap check: compute requested start/end
            DateTime reqStart = model.TripDate.Date.Add(model.TripStartTime);
            DateTime reqEnd   = reqStart.AddMinutes(duration);

            bool conflict = db.Bookings.Any(b =>
                b.BoatID == model.BoatID &&
                b.Status == BookingStatus.Confirmed &&
                DbFunctions.TruncateTime(b.TripDate) == model.TripDate.Date &&
                (
                    // existing full-day/booked-by-date entries (legacy) block the date
                    b.TripStartTime == TimeSpan.Zero ||
                    // otherwise check overlap using their TripStartTime (if present)
                    ( (b.TripDate.Add(b.TripStartTime) < reqEnd) &&
                      (b.TripDate.Add(b.TripStartTime).AddMinutes(boat.DefaultTripDurationMinutes) > reqStart)
                    )
                )
            );

            if (conflict)
                ModelState.AddModelError("", "The selected time slot is no longer available. Please choose a different time.");

            if (ModelState.IsValid)
            {
                int adults   = model.NumberOfPassengers;
                int children = model.ChildPassengers;
                model.CustomerID  = (int)Session["CustomerID"];
                model.Status      = BookingStatus.Confirmed;
                model.BookingDate = DateTime.Now;

                // Base price: passengers × per-person rates
                decimal basePrice = (boat.PriceAdult * adults) + (boat.PriceChild * children);

                // Extras: read selected extra IDs from form, look up prices
                var selectedExtraIds = (Request.Form.GetValues("selectedExtras") ?? new string[0])
                    .Select(s => { int x; return int.TryParse(s, out x) ? x : 0; })
                    .Where(x => x > 0)
                    .ToList();

                decimal extrasTotal = 0m;
                var extraNames = new System.Collections.Generic.List<string>();
                if (selectedExtraIds.Any())
                {
                    var extrasChosen = db.BoatExtras
                        .Where(e => e.BoatID == model.BoatID && selectedExtraIds.Contains(e.ExtraID))
                        .ToList();
                    extrasTotal = extrasChosen.Sum(e => e.Price);
                    extraNames  = extrasChosen.Select(e => e.Name + " (R" + e.Price.ToString("N2") + ")").ToList();
                }

                model.TotalPrice    = basePrice + extrasTotal;
                model.ExtrasTotal   = extrasTotal;
                model.SelectedExtras = extraNames.Any() ? string.Join(", ", extraNames) : null;
                model.IsRated       = false;

                db.Bookings.Add(model);
                db.SaveChanges();

                return RedirectToAction("BookingConfirmation", new { id = model.BookingID });
            }

            // Re-supply ViewBag so the form summary panel still renders correctly (and slots)
            // regenerate slots for the selected date
            var slots = new List<string>();
            if (!isUnavailable)
            {
                for (TimeSpan slot = harbourOpen; slot + TimeSpan.FromMinutes(duration) <= harbourClose; slot = slot.Add(TimeSpan.FromHours(1)))
                {
                    DateTime slotStart = model.TripDate.Date.Add(slot);
                    DateTime slotEnd   = slotStart.AddMinutes(duration);

                    bool overlap = db.Bookings.Any(b =>
                        b.BoatID == model.BoatID &&
                        b.Status == BookingStatus.Confirmed &&
                        DbFunctions.TruncateTime(b.TripDate) == model.TripDate.Date &&
                        (
                            b.TripStartTime == TimeSpan.Zero ||
                            (b.TripDate.Add(b.TripStartTime) < slotEnd &&
                             b.TripDate.Add(b.TripStartTime).AddMinutes(duration) > slotStart)
                        )
                    );

                    if (!overlap)
                        slots.Add(slot.ToString(@"hh\:mm"));
                }
            }

            ViewBag.AvailableSlots = slots;
            ViewBag.Boat    = boat;
            ViewBag.Extras  = boat.Extras.ToList();
            ViewBag.Weather = weatherService.GetWeather(boat.HarbourLocation);

            return View(model);
        }

        // Other actions...
    }
}