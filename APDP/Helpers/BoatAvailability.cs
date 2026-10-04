using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using APDP.Models;

namespace APDP.Helpers
{
    /// <summary>One weekday of a boat's timetable.</summary>
    public class DayHours
    {
        public int    Day          { get; set; }   // 0 = Sunday … 6 = Saturday
        public string Name         { get; set; }
        public bool   IsOpen       { get; set; }
        public int    OpenMinutes  { get; set; }
        public int    CloseMinutes { get; set; }

        public string OpenText  { get { return BoatAvailability.FormatTime(OpenMinutes); } }
        public string CloseText { get { return BoatAvailability.FormatTime(CloseMinutes); } }
    }

    /// <summary>One day in the customer's booking calendar.</summary>
    public class CalendarDay
    {
        public DateTime Date   { get; set; }
        public string   Status { get; set; }   // open | full | closed | blocked
    }

    /// <summary>
    /// Single source of truth for when a boat can be booked: its weekly timetable,
    /// blocked dates, trip-length limits, the break between trips and existing bookings.
    /// Used by the owner's boat form, the customer's booking page and the server-side checks.
    /// </summary>
    public static class BoatAvailability
    {
        /// <summary>Start times are offered every 30 minutes.</summary>
        public const int SlotStepMinutes = 30;

        /// <summary>Customers must book at least this far ahead of the start time.</summary>
        public const int MinimumNoticeMinutes = 30;

        /// <summary>How long an unpaid (Pending) booking keeps its slot.</summary>
        public static readonly TimeSpan PaymentHold = TimeSpan.FromMinutes(15);

        /// <summary>Monday-first display order.</summary>
        public static readonly int[] WeekOrder = { 1, 2, 3, 4, 5, 6, 0 };

        private static readonly string[] DayNames =
            { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        public static string FormatTime(int minutes)
        {
            return (minutes / 60).ToString("D2") + ":" + (minutes % 60).ToString("D2");
        }

        public static bool TryParseTime(string text, out int minutes)
        {
            minutes = 0;
            TimeSpan t;
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (text == "24:00") { minutes = 1440; return true; }
            if (!TimeSpan.TryParseExact(text, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out t))
                return false;
            minutes = (int)t.TotalMinutes;
            return true;
        }

        // ── Weekly timetable ─────────────────────────────────────────

        /// <summary>Suggested timetable for a new boat: Mon–Fri 08:00–16:00, Sat 09:00–13:00, Sun closed.</summary>
        public static List<DayHours> DefaultWeek()
        {
            return WeekOrder.Select(d => new DayHours
            {
                Day          = d,
                Name         = DayNames[d],
                IsOpen       = d != 0,
                OpenMinutes  = d == 6 ? 9 * 60 : 8 * 60,
                CloseMinutes = d == 6 ? 13 * 60 : 16 * 60
            }).ToList();
        }

        /// <summary>
        /// The boat's timetable. Boats created before timetables existed have no rows,
        /// so their single daily window applies to every day.
        /// </summary>
        public static List<DayHours> WeekFor(Boat boat, IEnumerable<BoatOpeningHours> rows)
        {
            var list = rows == null ? new List<BoatOpeningHours>() : rows.ToList();
            if (list.Count == 0)
            {
                int open  = boat.OperatingStartHour >= 0 ? boat.OperatingStartHour : 8;
                int close = boat.OperatingEndHour   >  0 ? boat.OperatingEndHour   : 16;
                if (close <= open) close = open + 1;
                return WeekOrder.Select(d => new DayHours
                {
                    Day = d, Name = DayNames[d], IsOpen = true,
                    OpenMinutes = open * 60, CloseMinutes = close * 60
                }).ToList();
            }

            return WeekOrder.Select(d =>
            {
                var row = list.FirstOrDefault(r => r.DayOfWeek == d);
                return new DayHours
                {
                    Day          = d,
                    Name         = DayNames[d],
                    IsOpen       = row != null && row.IsOpen,
                    OpenMinutes  = row != null ? row.OpenMinutes  : 8 * 60,
                    CloseMinutes = row != null ? row.CloseMinutes : 16 * 60
                };
            }).ToList();
        }

        public static List<DayHours> WeekFor(HarbourConnectContext db, Boat boat)
        {
            return WeekFor(boat, db.BoatOpeningHours.Where(h => h.BoatID == boat.BoatID).ToList());
        }

        /// <summary>
        /// Reads the timetable posted by the owner's boat form (fields dayN_open / dayN_from / dayN_to).
        /// Always returns the posted week (so the form can be shown again); <paramref name="error"/>
        /// is set when it is not valid.
        /// </summary>
        public static List<DayHours> ParseForm(NameValueCollection form, int minTripHours, out string error)
        {
            error = null;
            var week = new List<DayHours>();

            foreach (int d in WeekOrder)
            {
                bool isOpen = form["day" + d + "_open"] == "1";
                int from, to;
                bool okFrom = TryParseTime(form["day" + d + "_from"], out from);
                bool okTo   = TryParseTime(form["day" + d + "_to"],   out to);

                if (isOpen && error == null)
                {
                    if (!okFrom || !okTo)
                        error = $"Please choose opening and closing times for {DayNames[d]}.";
                    else if (to <= from)
                        error = $"On {DayNames[d]} the closing time must be later than the opening time.";
                    else if (to - from < minTripHours * 60)
                        error = $"On {DayNames[d]} the boat is open for less than the minimum trip of {minTripHours} hour(s).";
                }

                week.Add(new DayHours
                {
                    Day          = d,
                    Name         = DayNames[d],
                    IsOpen       = isOpen,
                    OpenMinutes  = okFrom ? from : 8 * 60,
                    CloseMinutes = okTo   ? to   : 16 * 60
                });
            }

            if (error == null && !week.Any(w => w.IsOpen))
                error = "Open the boat on at least one day of the week.";

            return week;
        }

        /// <summary>Replaces the boat's stored timetable with <paramref name="week"/> (caller saves).</summary>
        public static void SaveWeek(HarbourConnectContext db, Boat boat, List<DayHours> week)
        {
            var old = db.BoatOpeningHours.Where(h => h.BoatID == boat.BoatID).ToList();
            db.BoatOpeningHours.RemoveRange(old);

            foreach (var day in week)
            {
                db.BoatOpeningHours.Add(new BoatOpeningHours
                {
                    BoatID       = boat.BoatID,
                    DayOfWeek    = day.Day,
                    IsOpen       = day.IsOpen,
                    OpenMinutes  = day.OpenMinutes,
                    CloseMinutes = day.CloseMinutes
                });
            }

            // Keep the legacy single-window fields roughly in step (earliest open / latest close).
            var open = week.Where(w => w.IsOpen).ToList();
            boat.OperatingStartHour = open.Min(w => w.OpenMinutes) / 60;
            boat.OperatingEndHour   = (int)Math.Ceiling(open.Max(w => w.CloseMinutes) / 60.0);
        }

        // ── Trip length ──────────────────────────────────────────────

        public static int MinHours(Boat boat)
        {
            int mins = boat.DefaultTripDurationMinutes > 0 ? boat.DefaultTripDurationMinutes : 60;
            return (int)Math.Ceiling(mins / 60.0);
        }

        /// <summary>Owner's maximum, never longer than the longest open day.</summary>
        public static int MaxHours(Boat boat, List<DayHours> week)
        {
            int longestDay = week.Where(w => w.IsOpen)
                                 .Select(w => (w.CloseMinutes - w.OpenMinutes) / 60)
                                 .DefaultIfEmpty(0).Max();
            int max = boat.MaxBookingHours > 0 ? Math.Min(boat.MaxBookingHours, longestDay) : longestDay;
            return Math.Max(max, 0);
        }

        // ── Bookable start times ─────────────────────────────────────

        /// <summary>True for bookings that occupy their slot: confirmed, or unpaid but still inside the payment hold.</summary>
        public static bool HoldsSlot(Booking b, DateTime now)
        {
            return b.Status == BookingStatus.Confirmed ||
                   (b.Status == BookingStatus.Pending && b.BookingDate > now - PaymentHold);
        }

        /// <summary>
        /// Start times (minutes from midnight) a customer can book on <paramref name="date"/>
        /// for a trip of <paramref name="hours"/> hours. Empty when the day is closed, blocked, past or full.
        /// </summary>
        public static List<int> FreeStartTimes(HarbourConnectContext db, Boat boat, List<DayHours> week,
                                               DateTime date, int hours, int? ignoreBookingId = null)
        {
            var result = new List<int>();
            date = date.Date;
            DateTime now = DateTime.Now;

            if (date < now.Date) return result;
            if (hours < MinHours(boat) || hours > MaxHours(boat, week)) return result;

            var day = week.First(w => w.Day == (int)date.DayOfWeek);
            if (!day.IsOpen) return result;

            if (db.BoatUnavailableDates.Any(u => u.BoatID == boat.BoatID && u.Date == date)) return result;

            var taken = TakenRanges(db, boat.BoatID, date, date.AddDays(1), now, ignoreBookingId)
                .Where(t => t.Item1 == date).Select(t => t.Item2).ToList();

            int duration = hours * 60;
            int buffer   = Math.Max(boat.BufferMinutes, 0);
            int earliest = date == now.Date ? (int)now.TimeOfDay.TotalMinutes + MinimumNoticeMinutes : 0;

            for (int start = day.OpenMinutes; start + duration <= day.CloseMinutes; start += SlotStepMinutes)
            {
                if (start < earliest) continue;
                int end = start + duration;

                // Keep the break free on both sides of every existing trip.
                bool clash = taken.Any(r => start < r.Item2 + buffer && end + buffer > r.Item1);
                if (!clash) result.Add(start);
            }
            return result;
        }

        /// <summary>Status of each day from <paramref name="from"/> for the customer's date picker.</summary>
        public static List<CalendarDay> Calendar(HarbourConnectContext db, Boat boat, List<DayHours> week,
                                                 DateTime from, int days)
        {
            from = from.Date;
            DateTime to = from.AddDays(days);
            DateTime now = DateTime.Now;
            int minHours = MinHours(boat);

            var blocked = db.BoatUnavailableDates
                .Where(u => u.BoatID == boat.BoatID && u.Date >= from && u.Date < to)
                .Select(u => u.Date).ToList();
            var taken = TakenRanges(db, boat.BoatID, from, to, now, null);

            var list = new List<CalendarDay>();
            for (int i = 0; i < days; i++)
            {
                DateTime date = from.AddDays(i);
                var day = week.First(w => w.Day == (int)date.DayOfWeek);
                string status;

                if (!day.IsOpen)                         status = "closed";
                else if (blocked.Contains(date))         status = "blocked";
                else
                {
                    var ranges   = taken.Where(t => t.Item1 == date).Select(t => t.Item2).ToList();
                    int duration = minHours * 60;
                    int buffer   = Math.Max(boat.BufferMinutes, 0);
                    int earliest = date == now.Date ? (int)now.TimeOfDay.TotalMinutes + MinimumNoticeMinutes : 0;
                    bool anyFree = false;
                    for (int start = day.OpenMinutes; start + duration <= day.CloseMinutes && !anyFree; start += SlotStepMinutes)
                    {
                        if (start < earliest) continue;
                        int end = start + duration;
                        anyFree = !ranges.Any(r => start < r.Item2 + buffer && end + buffer > r.Item1);
                    }
                    status = anyFree ? "open" : "full";
                }

                list.Add(new CalendarDay { Date = date, Status = status });
            }
            return list;
        }

        /// <summary>(trip date, (start, end) minutes) for every booking that holds a slot in the range.</summary>
        private static List<Tuple<DateTime, Tuple<int, int>>> TakenRanges(
            HarbourConnectContext db, int boatId, DateTime from, DateTime to, DateTime now, int? ignoreBookingId)
        {
            DateTime holdCutoff = now - PaymentHold;
            int ignore = ignoreBookingId ?? 0;

            return db.Bookings
                .Where(b => b.BoatID == boatId
                         && b.BookingID != ignore
                         && b.TripDate >= from && b.TripDate < to
                         && (b.Status == BookingStatus.Confirmed
                             || (b.Status == BookingStatus.Pending && b.BookingDate > holdCutoff)))
                .Select(b => new { b.TripDate, b.TripStartMinutes, b.TripDurationHours })
                .ToList()
                .Select(b => Tuple.Create(b.TripDate.Date, Tuple.Create(
                    b.TripStartMinutes,
                    b.TripStartMinutes + (b.TripDurationHours > 0 ? b.TripDurationHours : 1) * 60)))
                .ToList();
        }
    }
}
