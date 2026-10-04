using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using APDP.Helpers;
using APDP.Models;

namespace APDP.Controllers
{
    public class BoatOwnerController : Controller
    {
        private HarbourConnectContext db = new HarbourConnectContext();

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
        public ActionResult Register(BoatOwner model)
        {
            if (ModelState.IsValid)
            {
                if (db.BoatOwners.Any(x => x.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "This email address is already registered.");
                    return View(model);
                }

                model.PasswordHash = PasswordHasher.Hash(model.Password);
                db.BoatOwners.Add(model);
                db.SaveChanges();

                // Clear any existing session so the new owner must log in fresh
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
            if (Session["BoatOwnerID"] != null)
                return RedirectToAction("Dashboard");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password)
        {
            var owner = db.BoatOwners.FirstOrDefault(x => x.Email == email);

            bool valid = owner != null && PasswordHasher.Verify(password, owner.PasswordHash);
            if (owner == null)
                PasswordHasher.VerifyDummy(password);

            if (valid)
            {
                // Clear any other portal sessions first
                Session.Remove("CustomerID");   Session.Remove("CustomerName");
                Session.Remove("DriverID");     Session.Remove("DriverName");
                Session.Remove("TnpaAdminID");  Session.Remove("TnpaAdminName");

                Session["BoatOwnerID"]   = owner.BoatOwnerID;
                Session["BoatOwnerName"] = owner.FullName;
                Session["UserType"]      = "BoatOwner";
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
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            int ownerID = (int)Session["BoatOwnerID"];

            // Single query - fetch statuses once, count in memory to avoid multiple round-trips
            var myBoatStatuses = db.Boats
                .Where(b => b.BoatOwnerID == ownerID)
                .Select(b => b.Status)
                .ToList();

            ViewBag.TotalBoats    = myBoatStatuses.Count;
            ViewBag.ApprovedBoats = myBoatStatuses.Count(s => s == BoatStatus.Active || s == BoatStatus.Approved);
            ViewBag.PendingBoats  = myBoatStatuses.Count(s => s == BoatStatus.Pending || s == BoatStatus.AwaitingPayment);
            ViewBag.TotalBookings = db.Bookings.Count(bk => bk.Boat.BoatOwnerID == ownerID);
            ViewBag.TotalDrivers  = db.Drivers.Count(d => d.BoatOwnerID == ownerID);

            return View();
        }

        // ─────────────────────────────────────────────
        // MY BOATS
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult MyBoats()
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            int ownerID = (int)Session["BoatOwnerID"];
            var boats = db.Boats.Where(b => b.BoatOwnerID == ownerID).ToList();
            return View(boats);
        }

        // ─────────────────────────────────────────────
        // TOGGLE ACTIVE / INACTIVE
        // ─────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleActive(int id)
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            int ownerID = (int)Session["BoatOwnerID"];
            var boat = db.Boats.FirstOrDefault(b => b.BoatID == id && b.BoatOwnerID == ownerID);

            if (boat == null)
            {
                TempData["ErrorMessage"] = "Boat not found.";
                return RedirectToAction("MyBoats");
            }

            // Only Active/Approved boats can be toggled
            if (boat.Status == BoatStatus.Active || boat.Status == BoatStatus.Approved)
            {
                boat.Status = BoatStatus.Inactive;
                TempData["SuccessMessage"] = $"'{boat.BoatName}' is now inactive and hidden from customers.";
            }
            else if (boat.Status == BoatStatus.Inactive)
            {
                boat.Status = BoatStatus.Active;
                TempData["SuccessMessage"] = $"'{boat.BoatName}' is now active and visible to customers.";
            }
            else
            {
                TempData["ErrorMessage"] = "Only active boats can be toggled. Pending and rejected boats cannot be changed this way.";
                return RedirectToAction("MyBoats");
            }

            db.SaveChanges();
            return RedirectToAction("MyBoats");
        }

        [HttpGet]
        public ActionResult AddBoat()
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            ViewBag.Week = BoatAvailability.DefaultWeek();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddBoat(Boat model,
            HttpPostedFileBase docOwnerId,
            HttpPostedFileBase docRegCert,
            HttpPostedFileBase docLicence)
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            if (!string.IsNullOrWhiteSpace(model.RegistrationNumber) &&
                db.Boats.Any(b => b.RegistrationNumber == model.RegistrationNumber))
            {
                ModelState.AddModelError("RegistrationNumber",
                    "This registration number is already in use. Each boat must have a unique registration number.");
            }

            string timetableError;
            var week = ReadTimetable(model, out timetableError);
            if (timetableError != null)
                ModelState.AddModelError("", timetableError);

            if (ModelState.IsValid)
            {
                string[] imgAllowed   = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                string[] vidAllowed   = { ".mp4", ".mov", ".avi", ".webm" };
                string[] docAllowed   = { ".pdf", ".jpg", ".jpeg", ".png" };
                string   folder       = Server.MapPath("~/Content/BoatImages/");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                Func<HttpPostedFileBase, string[], string> save = (file, allowed) =>
                {
                    if (file == null || file.ContentLength <= 0) return null;
                    string ext = Path.GetExtension(file.FileName).ToLower();
                    if (!Array.Exists(allowed, e => e == ext)) return null;
                    string fn = Guid.NewGuid().ToString() + ext;
                    file.SaveAs(Path.Combine(folder, fn));
                    return "~/Content/BoatImages/" + fn;
                };

                // Read all uploaded photos from the "boatPhotos" multi-file input
                var imgPaths = new System.Collections.Generic.List<string>();
                var photoFiles = Request.Files.GetMultiple("boatPhotos");
                foreach (HttpPostedFileBase f in photoFiles)
                {
                    if (imgPaths.Count >= 6) break;
                    var p = save(f, imgAllowed);
                    if (p != null) imgPaths.Add(p);
                }
                if (imgPaths.Count > 0) model.ImagePath  = imgPaths[0];
                if (imgPaths.Count > 1) model.ImagePath2 = imgPaths[1];
                if (imgPaths.Count > 2) model.ImagePath3 = imgPaths[2];
                if (imgPaths.Count > 3) model.ImagePath4 = imgPaths[3];
                if (imgPaths.Count > 4) model.ImagePath5 = imgPaths[4];
                if (imgPaths.Count > 5) model.ImagePath6 = imgPaths[5];

                // Read all uploaded videos from the "boatVideos" multi-file input
                var vidPaths = new System.Collections.Generic.List<string>();
                var videoFiles = Request.Files.GetMultiple("boatVideos");
                foreach (HttpPostedFileBase f in videoFiles)
                {
                    if (vidPaths.Count >= 3) break;
                    // Reject videos over 150 MB
                    if (f != null && f.ContentLength > 150 * 1024 * 1024) continue;
                    var p = save(f, vidAllowed);
                    if (p != null) vidPaths.Add(p);
                }
                if (vidPaths.Count > 0) model.VideoPath1 = vidPaths[0];
                if (vidPaths.Count > 1) model.VideoPath2 = vidPaths[1];
                if (vidPaths.Count > 2) model.VideoPath3 = vidPaths[2];

                model.OwnerIdDocumentPath      = save(docOwnerId,  docAllowed) ?? model.OwnerIdDocumentPath;
                model.BoatRegistrationCertPath = save(docRegCert,  docAllowed) ?? model.BoatRegistrationCertPath;
                model.BoatLicencePath          = save(docLicence,  docAllowed) ?? model.BoatLicencePath;

                model.BoatOwnerID   = (int)Session["BoatOwnerID"];
                model.Status        = BoatStatus.Pending;
                model.DateAdded     = DateTime.Now;
                model.AverageRating = 0;
                model.TotalRatings  = 0;

                // Apply defaults if duration tiles were not selected
                // (MaxBookingHours 0 = "No limit": up to the day's opening hours)
                if (model.DefaultTripDurationMinutes <= 0) model.DefaultTripDurationMinutes = 60;
                if (model.MaxBookingHours < 0)             model.MaxBookingHours = 0;

                db.Boats.Add(model);
                db.SaveChanges();

                BoatAvailability.SaveWeek(db, model, week);
                db.SaveChanges();

                // Notify all TNPA admins of new boat submission
                var tnpaAdmins = db.TnpaAdmins.ToList();
                foreach (var admin in tnpaAdmins)
                {
                    NotificationHelper.Notify(db,
                        admin.TnpaAdminID,
                        "TnpaAdmin",
                        $"New boat application submitted: '{model.BoatName}' by {((BoatOwner)db.BoatOwners.Find(model.BoatOwnerID)).FullName}. Registration: {model.RegistrationNumber}.",
                        "/Tnpa/BoatDetails/" + model.BoatID);
                }

                // Save extras posted as parallel arrays: Extras[i].Name + Extras[i].Price
                SaveExtras(model.BoatID, Request);
                db.SaveChanges();

                // Notify boat owner their submission was received
                NotificationHelper.Notify(db,
                    model.BoatOwnerID,
                    "BoatOwner",
                    $"Your boat '{model.BoatName}' has been submitted for TNPA review. You will be notified once reviewed.",
                    "/BoatOwner/MyBoats");
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Boat \"{model.BoatName}\" submitted for TNPA review.";
                return RedirectToAction("MyBoats");
            }

            ViewBag.Week = week;
            return View(model);
        }

        // ─────────────────────────────────────────────
        // EDIT BOAT
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult EditBoat(int? id)
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            if (id == null)
            {
                TempData["ErrorMessage"] = "No boat specified.";
                return RedirectToAction("MyBoats");
            }

            int ownerID = (int)Session["BoatOwnerID"];
            var boat = db.Boats
                .Include("Extras")
                .Include("BoatOwner")
                .Include("Ratings.Customer")
                .FirstOrDefault(b => b.BoatID == id && b.BoatOwnerID == ownerID
                                  && b.Status != BoatStatus.Removed);

            if (boat == null)
            {
                TempData["ErrorMessage"] = "Boat not found or access denied.";
                return RedirectToAction("MyBoats");
            }

            // Recent ratings for the preview panel
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditBoat(Boat model,
            HttpPostedFileBase eqLifeJacket,
            HttpPostedFileBase eqMedKit,
            HttpPostedFileBase eqFireExt,
            HttpPostedFileBase eqFishing,
            HttpPostedFileBase eqDecoration,
            HttpPostedFileBase eqSoundSystem)
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            int ownerID = (int)Session["BoatOwnerID"];
            var boat = db.Boats
                .Include("Extras")
                .FirstOrDefault(b => b.BoatID == model.BoatID && b.BoatOwnerID == ownerID
                                  && b.Status != BoatStatus.Removed);

            if (boat == null)
            {
                TempData["ErrorMessage"] = "Boat not found or access denied.";
                return RedirectToAction("MyBoats");
            }

            // Check for duplicate registration number BEFORE ModelState.IsValid
            if (!string.IsNullOrWhiteSpace(model.RegistrationNumber) &&
                db.Boats.Any(b => b.RegistrationNumber == model.RegistrationNumber && b.BoatID != model.BoatID))
            {
                ModelState.AddModelError("RegistrationNumber",
                    "This registration number is already in use by another boat.");
            }

            string timetableError;
            var week = ReadTimetable(model, out timetableError);
            if (timetableError != null)
                ModelState.AddModelError("", timetableError);

            if (ModelState.IsValid)
            {
                string[] allowed      = { ".jpg", ".jpeg", ".png", ".gif" };
                string   uploadFolder = Server.MapPath("~/Content/BoatImages/");
                if (!Directory.Exists(uploadFolder))
                    Directory.CreateDirectory(uploadFolder);

                // Helper: save file if provided, return virtual path or null
                Func<HttpPostedFileBase, string> save = (file) =>
                {
                    if (file == null || file.ContentLength <= 0) return null;
                    string ext = Path.GetExtension(file.FileName).ToLower();
                    if (!Array.Exists(allowed, e => e == ext)) return null;
                    string fn = Guid.NewGuid().ToString() + ext;
                    file.SaveAs(Path.Combine(uploadFolder, fn));
                    return "~/Content/BoatImages/" + fn;
                };

                // Boat photos - only replace if new files were uploaded
                string[] imgAllowedE = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                string[] vidAllowedE = { ".mp4", ".mov", ".avi", ".webm" };
                string   folderE     = Server.MapPath("~/Content/BoatImages/");

                Func<HttpPostedFileBase, string[], string> saveE = (file, exts) =>
                {
                    if (file == null || file.ContentLength <= 0) return null;
                    string ext = Path.GetExtension(file.FileName).ToLower();
                    if (!Array.Exists(exts, e => e == ext)) return null;
                    string fn = Guid.NewGuid().ToString() + ext;
                    file.SaveAs(Path.Combine(folderE, fn));
                    return "~/Content/BoatImages/" + fn;
                };

                var newImgPaths = new System.Collections.Generic.List<string>();
                foreach (HttpPostedFileBase f in Request.Files.GetMultiple("boatPhotos"))
                {
                    if (newImgPaths.Count >= 6) break;
                    var p = saveE(f, imgAllowedE);
                    if (p != null) newImgPaths.Add(p);
                }
                if (newImgPaths.Count > 0) boat.ImagePath  = newImgPaths[0];
                if (newImgPaths.Count > 1) boat.ImagePath2 = newImgPaths[1];
                if (newImgPaths.Count > 2) boat.ImagePath3 = newImgPaths[2];
                if (newImgPaths.Count > 3) boat.ImagePath4 = newImgPaths[3];
                if (newImgPaths.Count > 4) boat.ImagePath5 = newImgPaths[4];
                if (newImgPaths.Count > 5) boat.ImagePath6 = newImgPaths[5];

                var newVidPaths = new System.Collections.Generic.List<string>();
                foreach (HttpPostedFileBase f in Request.Files.GetMultiple("boatVideos"))
                {
                    if (newVidPaths.Count >= 3) break;
                    // Reject videos over 150 MB
                    if (f != null && f.ContentLength > 150 * 1024 * 1024) continue;
                    var p = saveE(f, vidAllowedE);
                    if (p != null) newVidPaths.Add(p);
                }
                if (newVidPaths.Count > 0) boat.VideoPath1 = newVidPaths[0];
                if (newVidPaths.Count > 1) boat.VideoPath2 = newVidPaths[1];
                if (newVidPaths.Count > 2) boat.VideoPath3 = newVidPaths[2];

                // Equipment photos - only replace if a new file was uploaded
                boat.LifeJacketImagePath  = save(eqLifeJacket)  ?? boat.LifeJacketImagePath;
                boat.MedKitImagePath      = save(eqMedKit)       ?? boat.MedKitImagePath;
                boat.FireExtImagePath     = save(eqFireExt)      ?? boat.FireExtImagePath;
                boat.FishingImagePath     = save(eqFishing)      ?? boat.FishingImagePath;
                boat.DecorationImagePath  = save(eqDecoration)   ?? boat.DecorationImagePath;
                boat.SoundSystemImagePath = save(eqSoundSystem)  ?? boat.SoundSystemImagePath;

                bool wasActive = (boat.Status == BoatStatus.Active || boat.Status == BoatStatus.Approved);

                bool requiresReview = (boat.RegistrationNumber != model.RegistrationNumber ||
                                       boat.BoatName != model.BoatName);

                boat.BoatName                = model.BoatName;
                boat.RegistrationNumber      = model.RegistrationNumber;
                boat.BoatType                = model.BoatType;
                boat.Description             = model.Description;
                boat.MaxPassengers           = model.MaxPassengers;

                // Save new per-hour pricing
                boat.PriceAdult              = model.PriceAdult;
                boat.PriceChild              = model.PriceChild;

                // Save duration limits
                boat.DefaultTripDurationMinutes = model.DefaultTripDurationMinutes > 0 ? model.DefaultTripDurationMinutes : 60;
                boat.MaxBookingHours            = model.MaxBookingHours > 0 ? model.MaxBookingHours : 0;

                // Weekly timetable + break between trips
                boat.BufferMinutes = model.BufferMinutes;
                BoatAvailability.SaveWeek(db, boat, week);

                boat.HarbourLocation         = !string.IsNullOrWhiteSpace(model.HarbourLocation)
                                                   ? model.HarbourLocation
                                                   : boat.HarbourLocation;

                // Safety equipment flags
                boat.HasLifeJackets          = model.HasLifeJackets;
                boat.HasMedKit               = model.HasMedKit;
                boat.HasFireExtinguisher     = model.HasFireExtinguisher;

                boat.HasFishingEquipment     = model.HasFishingEquipment;
                boat.HasDecoration           = model.HasDecoration;
                boat.HasSoundSystem          = model.HasSoundSystem;
                boat.DisabilityAccommodation = model.DisabilityAccommodation;
                boat.IsDisabilityFriendly    = model.IsDisabilityFriendly;

                if (requiresReview)
                {
                    // Name / registration number are what TNPA approved, so any change
                    // takes the boat off the market until TNPA re-approves it.
                    boat.Status = BoatStatus.Pending;
                    foreach (var admin in db.TnpaAdmins.ToList())
                    {
                        NotificationHelper.Notify(db,
                            admin.TnpaAdminID,
                            "TnpaAdmin",
                            $"Boat '{boat.BoatName}' (Reg: {boat.RegistrationNumber}) changed its name or registration number and needs re-approval.",
                            "/Tnpa/BoatDetails/" + boat.BoatID);
                    }
                    TempData["SuccessMessage"] = wasActive
                        ? "Boat updated. Because the name or registration number changed, it is hidden from customers until TNPA re-approves it."
                        : "Boat updated. Because key details changed, it has been resubmitted for TNPA review.";
                }
                else if (wasActive)
                {
                    // Boat stays Active so customers can still see it,
                    // but TNPA is notified to review the changes.
                    boat.Status = BoatStatus.Active;
                    var tnpaAdmins = db.TnpaAdmins.ToList();
                    foreach (var admin in tnpaAdmins)
                    {
                        NotificationHelper.Notify(db,
                            admin.TnpaAdminID,
                            "TnpaAdmin",
                            $"Boat '{boat.BoatName}' (Reg: {boat.RegistrationNumber}) was edited by the owner and requires TNPA review.",
                            "/Tnpa/BoatDetails/" + boat.BoatID);
                    }
                    TempData["SuccessMessage"] = "Boat updated. TNPA has been notified to review your changes.";
                }
                else
                {
                    TempData["SuccessMessage"] = "Boat updated successfully.";
                }

                db.SaveChanges();

                // Replace all extras with what was posted
                var oldExtras = db.BoatExtras.Where(e => e.BoatID == boat.BoatID).ToList();
                db.BoatExtras.RemoveRange(oldExtras);
                db.SaveChanges();
                SaveExtras(boat.BoatID, Request);

                return RedirectToAction("MyBoats");
            }

            // Re-load the full boat entity from DB so image paths are still available
            // on the view even when ModelState is invalid (form-bound model has null image paths).
            var boatForView = db.Boats
                .Include("Extras")
                .FirstOrDefault(b => b.BoatID == model.BoatID && b.BoatOwnerID == ownerID);

            if (boatForView != null)
            {
                // Copy posted values onto the DB entity so the form retains what the user typed
                boatForView.BoatName                 = model.BoatName;
                boatForView.RegistrationNumber       = model.RegistrationNumber;
                boatForView.BoatType                 = model.BoatType;
                boatForView.Description              = model.Description;
                boatForView.MaxPassengers            = model.MaxPassengers;
                boatForView.PriceAdult               = model.PriceAdult;
                boatForView.PriceChild               = model.PriceChild;
                boatForView.DefaultTripDurationMinutes = model.DefaultTripDurationMinutes;
                boatForView.MaxBookingHours          = model.MaxBookingHours;
                boatForView.BufferMinutes            = model.BufferMinutes;
                boatForView.HarbourLocation          = model.HarbourLocation;
                boatForView.HasLifeJackets           = model.HasLifeJackets;
                boatForView.HasMedKit                = model.HasMedKit;
                boatForView.HasFireExtinguisher      = model.HasFireExtinguisher;
                boatForView.HasFishingEquipment      = model.HasFishingEquipment;
                boatForView.HasDecoration            = model.HasDecoration;
                boatForView.HasSoundSystem           = model.HasSoundSystem;
                boatForView.IsDisabilityFriendly     = model.IsDisabilityFriendly;
                boatForView.DisabilityAccommodation  = model.DisabilityAccommodation;

                ViewBag.RecentRatings = db.BoatRatings
                    .Include("Customer")
                    .Where(r => r.BoatID == model.BoatID)
                    .OrderByDescending(r => r.DateRated)
                    .Take(5)
                    .ToList();
                ViewBag.Week = week;
                return View(boatForView);
            }

            ViewBag.Week = week;
            return View(model);
        }

        // Reads the weekly timetable posted by the Add/Edit Boat form and checks the trip-length range.
        private List<DayHours> ReadTimetable(Boat model, out string error)
        {
            int minHours = BoatAvailability.MinHours(model);
            var week = BoatAvailability.ParseForm(Request.Form, minHours, out error);
            if (error == null && model.MaxBookingHours > 0 && model.MaxBookingHours < minHours)
                error = "The maximum trip length must be at least the minimum trip length.";
            return week;
        }

        // ─────────────────────────────────────────────
        // BOOKINGS
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult Bookings()
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            int ownerID = (int)Session["BoatOwnerID"];
            var bookings = db.Bookings
                .Include("Boat")
                .Include("Customer")
                .Where(bk => bk.Boat.BoatOwnerID == ownerID)
                .OrderByDescending(bk => bk.BookingDate)
                .ToList();

            return View(bookings);
        }

        // ─────────────────────────────────────────────
        // MANAGE DRIVERS
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult ManageDrivers()
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            int ownerID = (int)Session["BoatOwnerID"];
            var drivers = db.Drivers
                .Include("AssignedBoat")
                .Where(d => d.BoatOwnerID == ownerID)
                .ToList();

            // Pass owner's boats for assignment dropdown
            ViewBag.OwnerBoats = db.Boats
                .Where(b => b.BoatOwnerID == ownerID && (b.Status == BoatStatus.Active || b.Status == BoatStatus.Approved))
                .ToList();

            return View(drivers);
        }

        // ─────────────────────────────────────────────
        // ADD DRIVER
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult AddDriver()
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            int ownerID = (int)Session["BoatOwnerID"];
            ViewBag.OwnerBoats = new SelectList(
                db.Boats.Where(b => b.BoatOwnerID == ownerID && (b.Status == BoatStatus.Active || b.Status == BoatStatus.Approved)).ToList(),
                "BoatID", "BoatName");

            return View(new Driver());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddDriver(Driver model)
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            int ownerID = (int)Session["BoatOwnerID"];

            if (db.Drivers.Any(d => d.Email == model.Email))
            {
                ModelState.AddModelError("Email", "A driver with this email already exists.");
            }

            if (!IsOwnBoat(model.AssignedBoatID, ownerID))
                ModelState.AddModelError("AssignedBoatID", "You can only assign a driver to one of your own active boats.");

            if (ModelState.IsValid)
            {
                model.BoatOwnerID     = ownerID;
                model.Status          = DriverStatus.Active;
                model.DateRegistered  = DateTime.Now;
                model.PasswordHash    = PasswordHasher.Hash(model.Password);

                db.Drivers.Add(model);
                db.SaveChanges();

                TempData["DriverAdded"] = model.FullName;
                TempData["DriverEmail"] = model.Email;
                return RedirectToAction("ManageDrivers");
            }

            ViewBag.OwnerBoats = new SelectList(
                db.Boats.Where(b => b.BoatOwnerID == ownerID && (b.Status == BoatStatus.Active || b.Status == BoatStatus.Approved)).ToList(),
                "BoatID", "BoatName");

            return View(model);
        }

        // ─────────────────────────────────────────────
        // EDIT DRIVER
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult EditDriver(int? id)
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            if (id == null)
            {
                TempData["ErrorMessage"] = "No driver specified.";
                return RedirectToAction("ManageDrivers");
            }

            int ownerID = (int)Session["BoatOwnerID"];
            var driver = db.Drivers.FirstOrDefault(d => d.DriverID == id && d.BoatOwnerID == ownerID);

            if (driver == null)
            {
                TempData["ErrorMessage"] = "Driver not found or access denied.";
                return RedirectToAction("ManageDrivers");
            }

            ViewBag.OwnerBoats = new SelectList(
                db.Boats.Where(b => b.BoatOwnerID == ownerID && (b.Status == BoatStatus.Active || b.Status == BoatStatus.Approved)).ToList(),
                "BoatID", "BoatName", driver.AssignedBoatID);

            return View(driver);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditDriver(Driver model)
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            int ownerID = (int)Session["BoatOwnerID"];
            var driver = db.Drivers.FirstOrDefault(d => d.DriverID == model.DriverID && d.BoatOwnerID == ownerID);

            if (driver == null)
            {
                TempData["ErrorMessage"] = "Driver not found or access denied.";
                return RedirectToAction("ManageDrivers");
            }

            // Email uniqueness check (allow same driver to keep their email)
            if (db.Drivers.Any(d => d.Email == model.Email && d.DriverID != model.DriverID))
            {
                ModelState.AddModelError("Email", "Another driver with this email already exists.");
            }

            if (!IsOwnBoat(model.AssignedBoatID, ownerID))
                ModelState.AddModelError("AssignedBoatID", "You can only assign a driver to one of your own active boats.");

            // Password is optional on edit; blank means keep the current one.
            if (string.IsNullOrWhiteSpace(model.Password))
                ModelState.Remove("Password");

            if (ModelState.IsValid)
            {
                driver.FullName        = model.FullName;
                driver.Email           = model.Email;
                driver.PhoneNumber     = model.PhoneNumber;
                driver.LicenseNumber   = model.LicenseNumber;
                driver.Status          = model.Status;
                driver.AssignedBoatID  = model.AssignedBoatID;

                // Only update password if a new one was provided
                if (!string.IsNullOrWhiteSpace(model.Password))
                    driver.PasswordHash = PasswordHasher.Hash(model.Password);

                db.SaveChanges();

                TempData["SuccessMessage"] = "Driver details updated successfully.";
                return RedirectToAction("ManageDrivers");
            }

            ViewBag.OwnerBoats = new SelectList(
                db.Boats.Where(b => b.BoatOwnerID == ownerID && (b.Status == BoatStatus.Active || b.Status == BoatStatus.Approved)).ToList(),
                "BoatID", "BoatName", model.AssignedBoatID);

            return View(model);
        }

        // ─────────────────────────────────────────────
        // PAY REGISTRATION FEE
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult PayRegistrationFee(int? id)
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            if (id == null)
            {
                TempData["ErrorMessage"] = "No boat specified.";
                return RedirectToAction("MyBoats");
            }

            int ownerID = (int)Session["BoatOwnerID"];
            var boat = db.Boats.FirstOrDefault(b => b.BoatID == id
                                                 && b.BoatOwnerID == ownerID
                                                 && b.Status == BoatStatus.AwaitingPayment);

            if (boat == null)
            {
                TempData["ErrorMessage"] = "Boat not found or not eligible for payment.";
                return RedirectToAction("MyBoats");
            }

            return View(boat);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PayRegistrationFee(int? id, FormCollection form)
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            if (id == null)
                return RedirectToAction("MyBoats");

            int ownerID = (int)Session["BoatOwnerID"];
            var boat = db.Boats.FirstOrDefault(b => b.BoatID == id
                                                 && b.BoatOwnerID == ownerID
                                                 && b.Status == BoatStatus.AwaitingPayment);

            if (boat == null)
            {
                TempData["ErrorMessage"] = "Boat not found or payment already completed.";
                return RedirectToAction("MyBoats");
            }

            // Generate unique certificate number
            string certNumber = "HC-" + DateTime.Now.Year
                              + "-" + boat.BoatID.ToString("D5")
                              + "-" + new Random().Next(100, 999).ToString();

            // DEMO PAYMENT: replace with a real payment provider (e.g. PayFast hosted checkout)
            // before going live. No card data is ever collected by this app.
            boat.Status                       = BoatStatus.Active;
            boat.CertificateNumber            = certNumber;
            boat.RegistrationPaidDate         = DateTime.Now;
            boat.RegistrationPaymentReference = "DEMO-REG-" + boat.BoatID.ToString("D5") + "-" + DateTime.Now.ToString("HHmmss");

            db.SaveChanges();

            // Notify boat owner payment confirmed + boat is live
            NotificationHelper.Notify(db,
                boat.BoatOwnerID,
                "BoatOwner",
                $"Payment confirmed! Your boat '{boat.BoatName}' is now active and visible to customers. Certificate: {certNumber}",
                "/BoatOwner/RegistrationCertificate/" + boat.BoatID);

            // Notify all TNPA admins that the boat is now live
            var tnpaAdminsP = db.TnpaAdmins.ToList();
            foreach (var admin in tnpaAdminsP)
            {
                NotificationHelper.Notify(db,
                    admin.TnpaAdminID,
                    "TnpaAdmin",
                    $"Boat '{boat.BoatName}' (Reg: {boat.RegistrationNumber}) has paid the registration fee and is now active.",
                    "/Tnpa/BoatDetails/" + boat.BoatID);
            }
            db.SaveChanges();

            TempData["SuccessMessage"] = $"Payment successful! Your boat '{boat.BoatName}' is now active and visible to customers.";
            return RedirectToAction("RegistrationCertificate", new { id = boat.BoatID });
        }

        // ─────────────────────────────────────────────
        // REGISTRATION CERTIFICATE
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult RegistrationCertificate(int? id)
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            if (id == null)
                return RedirectToAction("MyBoats");

            int ownerID = (int)Session["BoatOwnerID"];
            var boat = db.Boats
                .Include("BoatOwner")
                .FirstOrDefault(b => b.BoatID == id
                                  && b.BoatOwnerID == ownerID
                                  && b.Status == BoatStatus.Active
                                  && b.CertificateNumber != null);

            if (boat == null)
            {
                TempData["ErrorMessage"] = "Certificate not found.";
                return RedirectToAction("MyBoats");
            }

            return View(boat);
        }

        // No boat (unassigned) is allowed; otherwise it must be this owner's active/approved boat.
        private bool IsOwnBoat(int? boatId, int ownerID)
        {
            if (boatId == null) return true;
            return db.Boats.Any(b => b.BoatID == boatId
                                  && b.BoatOwnerID == ownerID
                                  && (b.Status == BoatStatus.Active || b.Status == BoatStatus.Approved));
        }

        // ─────────────────────────────────────────────
        // HELPER - SAVE EXTRAS FROM REQUEST
        // ─────────────────────────────────────────────

        private void SaveExtras(int boatId, HttpRequestBase request)
        {
            // The form posts parallel arrays: extraName[] and extraPrice[]
            var names  = request.Form.GetValues("extraName");
            var prices = request.Form.GetValues("extraPrice");

            if (names == null || prices == null) return;

            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i]?.Trim();
                if (string.IsNullOrEmpty(name)) continue;

                decimal price = 0;
                decimal.TryParse(prices.Length > i ? prices[i] : "0",
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out price);

                db.BoatExtras.Add(new BoatExtra
                {
                    BoatID = boatId,
                    Name   = name,
                    Price  = price < 0 ? 0 : price
                });
            }
            db.SaveChanges();
        }

        // ─────────────────────────────────────────────
        // DISPOSE
        // ─────────────────────────────────────────────

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }

        // ─────────────────────────────────────────────
        // MANAGE UNAVAILABLE DATES
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult ManageAvailability(int? id)
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            if (id == null)
                return RedirectToAction("MyBoats");

            int ownerID = (int)Session["BoatOwnerID"];
            var boat = db.Boats.FirstOrDefault(b => b.BoatID == id && b.BoatOwnerID == ownerID);
            if (boat == null)
            {
                TempData["ErrorMessage"] = "Boat not found or access denied.";
                return RedirectToAction("MyBoats");
            }

            var unavailable = db.BoatUnavailableDates
                .Where(u => u.BoatID == boat.BoatID)
                .OrderBy(u => u.Date)
                .ToList();

            ViewBag.Boat = boat;
            return View(unavailable);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddUnavailableDate(int boatId, DateTime date, string reason)
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            int ownerID = (int)Session["BoatOwnerID"];
            var boat = db.Boats.FirstOrDefault(b => b.BoatID == boatId && b.BoatOwnerID == ownerID);
            if (boat == null)
            {
                TempData["ErrorMessage"] = "Boat not found or access denied.";
                return RedirectToAction("MyBoats");
            }

            date = date.Date;
            if (!db.BoatUnavailableDates.Any(u => u.BoatID == boatId && u.Date == date))
            {
                db.BoatUnavailableDates.Add(new BoatUnavailableDate
                {
                    BoatID = boatId,
                    Date = date,
                    Reason = reason
                });
                db.SaveChanges();
            }

            return RedirectToAction("ManageAvailability", new { id = boatId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveUnavailableDate(int id)
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            var u = db.BoatUnavailableDates.Find(id);
            if (u == null)
                return RedirectToAction("MyBoats");

            int ownerID = (int)Session["BoatOwnerID"];
            var boat = db.Boats.Find(u.BoatID);
            if (boat == null || boat.BoatOwnerID != ownerID)
                return RedirectToAction("MyBoats");

            db.BoatUnavailableDates.Remove(u);
            db.SaveChanges();
            return RedirectToAction("ManageAvailability", new { id = boat.BoatID });
        }
    }
}
