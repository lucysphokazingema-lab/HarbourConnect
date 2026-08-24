using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
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

                db.BoatOwners.Add(model);
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
            if (Session["BoatOwnerID"] != null)
                return RedirectToAction("Dashboard");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password)
        {
            var owner = db.BoatOwners
                .FirstOrDefault(x => x.Email == email && x.Password == password);

            if (owner != null)
            {
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

            // Single query — fetch statuses once, count in memory to avoid multiple round-trips
            var myBoatStatuses = db.Boats
                .Where(b => b.BoatOwnerID == ownerID)
                .Select(b => b.Status)
                .ToList();

            ViewBag.TotalBoats    = myBoatStatuses.Count;
            ViewBag.ApprovedBoats = myBoatStatuses.Count(s => s == BoatStatus.Approved);
            ViewBag.PendingBoats  = myBoatStatuses.Count(s => s == BoatStatus.Pending);
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
        // ADD BOAT
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult AddBoat()
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddBoat(Boat model,
            HttpPostedFileBase imageFile,
            HttpPostedFileBase imageFile2,
            HttpPostedFileBase imageFile3,
            HttpPostedFileBase eqLifeJacket,
            HttpPostedFileBase eqMedKit,
            HttpPostedFileBase eqFireExt,
            HttpPostedFileBase eqFishing,
            HttpPostedFileBase eqDecoration,
            HttpPostedFileBase eqSoundSystem,
            HttpPostedFileBase docOwnerId,
            HttpPostedFileBase docRegCert,
            HttpPostedFileBase docLicence)
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            // Check for duplicate registration number BEFORE ModelState.IsValid
            // so the error always shows regardless of other validation failures
            if (!string.IsNullOrWhiteSpace(model.RegistrationNumber) &&
                db.Boats.Any(b => b.RegistrationNumber == model.RegistrationNumber))
            {
                ModelState.AddModelError("RegistrationNumber",
                    "This registration number is already in use. Each boat must have a unique registration number.");
            }

            if (ModelState.IsValid)
            {
                string[] imgAllowed = { ".jpg", ".jpeg", ".png", ".gif" };
                string[] docAllowed = { ".pdf", ".jpg", ".jpeg", ".png" };
                string   folder     = Server.MapPath("~/Content/BoatImages/");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                // Save image helper
                Func<HttpPostedFileBase, string[], string> save = (file, allowed) =>
                {
                    if (file == null || file.ContentLength <= 0) return null;
                    string ext = Path.GetExtension(file.FileName).ToLower();
                    if (!Array.Exists(allowed, e => e == ext)) return null;
                    string fn = Guid.NewGuid().ToString() + ext;
                    file.SaveAs(Path.Combine(folder, fn));
                    return "~/Content/BoatImages/" + fn;
                };

                // Boat photos
                model.ImagePath  = save(imageFile,  imgAllowed) ?? model.ImagePath;
                model.ImagePath2 = save(imageFile2, imgAllowed) ?? model.ImagePath2;
                model.ImagePath3 = save(imageFile3, imgAllowed) ?? model.ImagePath3;

                // Equipment photos
                model.LifeJacketImagePath  = save(eqLifeJacket,  imgAllowed) ?? model.LifeJacketImagePath;
                model.MedKitImagePath      = save(eqMedKit,       imgAllowed) ?? model.MedKitImagePath;
                model.FireExtImagePath     = save(eqFireExt,      imgAllowed) ?? model.FireExtImagePath;
                model.FishingImagePath     = save(eqFishing,      imgAllowed) ?? model.FishingImagePath;
                model.DecorationImagePath  = save(eqDecoration,   imgAllowed) ?? model.DecorationImagePath;
                model.SoundSystemImagePath = save(eqSoundSystem,  imgAllowed) ?? model.SoundSystemImagePath;

                // Supporting documents (PDF or image)
                model.OwnerIdDocumentPath    = save(docOwnerId,  docAllowed) ?? model.OwnerIdDocumentPath;
                model.BoatRegistrationCertPath = save(docRegCert, docAllowed) ?? model.BoatRegistrationCertPath;
                model.BoatLicencePath        = save(docLicence,  docAllowed) ?? model.BoatLicencePath;

                model.BoatOwnerID   = (int)Session["BoatOwnerID"];
                model.Status        = BoatStatus.Pending;
                model.DateAdded     = DateTime.Now;
                model.AverageRating = 0;
                model.TotalRatings  = 0;

                db.Boats.Add(model);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Boat \"{model.BoatName}\" submitted successfully! Registration: {model.RegistrationNumber}. It is now pending TNPA approval.";
                return RedirectToAction("MyBoats");
            }

            return View(model);
        }

        // ─────────────────────────────────────────────
        // EDIT BOAT
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult EditBoat(int id)
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            int ownerID = (int)Session["BoatOwnerID"];
            var boat = db.Boats.FirstOrDefault(b => b.BoatID == id && b.BoatOwnerID == ownerID);

            if (boat == null)
            {
                TempData["ErrorMessage"] = "Boat not found or access denied.";
                return RedirectToAction("MyBoats");
            }

            return View(boat);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditBoat(Boat model,
            HttpPostedFileBase imageFile,
            HttpPostedFileBase imageFile2,
            HttpPostedFileBase imageFile3,
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
            var boat = db.Boats.FirstOrDefault(b => b.BoatID == model.BoatID && b.BoatOwnerID == ownerID);

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

                // Boat photos — only replace if a new file was uploaded
                boat.ImagePath  = save(imageFile)  ?? boat.ImagePath;
                boat.ImagePath2 = save(imageFile2) ?? boat.ImagePath2;
                boat.ImagePath3 = save(imageFile3) ?? boat.ImagePath3;

                // Equipment photos — only replace if a new file was uploaded
                boat.LifeJacketImagePath  = save(eqLifeJacket)  ?? boat.LifeJacketImagePath;
                boat.MedKitImagePath      = save(eqMedKit)       ?? boat.MedKitImagePath;
                boat.FireExtImagePath     = save(eqFireExt)      ?? boat.FireExtImagePath;
                boat.FishingImagePath     = save(eqFishing)      ?? boat.FishingImagePath;
                boat.DecorationImagePath  = save(eqDecoration)   ?? boat.DecorationImagePath;
                boat.SoundSystemImagePath = save(eqSoundSystem)  ?? boat.SoundSystemImagePath;

                bool requiresReview = (boat.RegistrationNumber != model.RegistrationNumber ||
                                       boat.BoatName != model.BoatName);

                boat.BoatName                = model.BoatName;
                boat.RegistrationNumber      = model.RegistrationNumber;
                boat.BoatType                = model.BoatType;
                boat.Description             = model.Description;
                boat.MaxPassengers           = model.MaxPassengers;
                boat.PricePerTrip            = model.PricePerTrip;
                boat.HarbourLocation         = model.HarbourLocation;
                boat.LifeJacketQuantity      = model.LifeJacketQuantity;
                boat.HasMedKit               = model.HasMedKit;
                boat.HasFireExtinguisher     = model.HasFireExtinguisher;
                boat.HasFishingEquipment     = model.HasFishingEquipment;
                boat.HasDecoration           = model.HasDecoration;
                boat.HasSoundSystem          = model.HasSoundSystem;
                boat.DisabilityAccommodation = model.DisabilityAccommodation;

                if (requiresReview)
                {
                    boat.Status = BoatStatus.Pending;
                    TempData["SuccessMessage"] = "Boat updated. Because key details changed, it has been resubmitted for TNPA review.";
                }
                else
                {
                    TempData["SuccessMessage"] = "Boat updated successfully.";
                }

                db.SaveChanges();
                return RedirectToAction("MyBoats");
            }

            return View(model);
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
                .Where(b => b.BoatOwnerID == ownerID && b.Status == BoatStatus.Approved)
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
                db.Boats.Where(b => b.BoatOwnerID == ownerID && b.Status == BoatStatus.Approved).ToList(),
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

            if (ModelState.IsValid)
            {
                model.BoatOwnerID     = ownerID;
                model.Status          = DriverStatus.Active;
                model.DateRegistered  = DateTime.Now;

                db.Drivers.Add(model);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Driver {model.FullName} has been added. They can now log in with email: {model.Email}";
                return RedirectToAction("ManageDrivers");
            }

            ViewBag.OwnerBoats = new SelectList(
                db.Boats.Where(b => b.BoatOwnerID == ownerID && b.Status == BoatStatus.Approved).ToList(),
                "BoatID", "BoatName");

            return View(model);
        }

        // ─────────────────────────────────────────────
        // EDIT DRIVER
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult EditDriver(int id)
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            int ownerID = (int)Session["BoatOwnerID"];
            var driver = db.Drivers.FirstOrDefault(d => d.DriverID == id && d.BoatOwnerID == ownerID);

            if (driver == null)
            {
                TempData["ErrorMessage"] = "Driver not found or access denied.";
                return RedirectToAction("ManageDrivers");
            }

            ViewBag.OwnerBoats = new SelectList(
                db.Boats.Where(b => b.BoatOwnerID == ownerID && b.Status == BoatStatus.Approved).ToList(),
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
                    driver.Password = model.Password;

                db.SaveChanges();

                TempData["SuccessMessage"] = "Driver details updated successfully.";
                return RedirectToAction("ManageDrivers");
            }

            ViewBag.OwnerBoats = new SelectList(
                db.Boats.Where(b => b.BoatOwnerID == ownerID && b.Status == BoatStatus.Approved).ToList(),
                "BoatID", "BoatName", model.AssignedBoatID);

            return View(model);
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
    }
}
