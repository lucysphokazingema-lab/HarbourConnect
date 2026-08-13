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

            ViewBag.TotalBoats    = db.Boats.Count(b => b.BoatOwnerID == ownerID);
            ViewBag.ApprovedBoats = db.Boats.Count(b => b.BoatOwnerID == ownerID && b.Status == BoatStatus.Approved);
            ViewBag.PendingBoats  = db.Boats.Count(b => b.BoatOwnerID == ownerID && b.Status == BoatStatus.Pending);
            ViewBag.TotalBookings = db.Bookings
                .Count(bk => bk.Boat.BoatOwnerID == ownerID);

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
        public ActionResult AddBoat(Boat model, HttpPostedFileBase imageFile, HttpPostedFileBase imageFile2, HttpPostedFileBase imageFile3)
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            if (ModelState.IsValid)
            {
                string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
                string uploadsFolder = Server.MapPath("~/Content/BoatImages/");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                // Main image
                if (imageFile != null && imageFile.ContentLength > 0)
                {
                    string ext = Path.GetExtension(imageFile.FileName).ToLower();
                    if (!Array.Exists(allowedExtensions, e => e == ext))
                    {
                        ModelState.AddModelError("ImagePath", "Only JPG, PNG or GIF images are allowed.");
                        return View(model);
                    }
                    string fileName = Guid.NewGuid().ToString() + ext;
                    imageFile.SaveAs(Path.Combine(uploadsFolder, fileName));
                    model.ImagePath = "~/Content/BoatImages/" + fileName;
                }

                // Side view image
                if (imageFile2 != null && imageFile2.ContentLength > 0)
                {
                    string ext = Path.GetExtension(imageFile2.FileName).ToLower();
                    if (!Array.Exists(allowedExtensions, e => e == ext))
                    {
                        ModelState.AddModelError("ImagePath2", "Only JPG, PNG or GIF images are allowed.");
                        return View(model);
                    }
                    string fileName = Guid.NewGuid().ToString() + ext;
                    imageFile2.SaveAs(Path.Combine(uploadsFolder, fileName));
                    model.ImagePath2 = "~/Content/BoatImages/" + fileName;
                }

                // Interior image
                if (imageFile3 != null && imageFile3.ContentLength > 0)
                {
                    string ext = Path.GetExtension(imageFile3.FileName).ToLower();
                    if (!Array.Exists(allowedExtensions, e => e == ext))
                    {
                        ModelState.AddModelError("ImagePath3", "Only JPG, PNG or GIF images are allowed.");
                        return View(model);
                    }
                    string fileName = Guid.NewGuid().ToString() + ext;
                    imageFile3.SaveAs(Path.Combine(uploadsFolder, fileName));
                    model.ImagePath3 = "~/Content/BoatImages/" + fileName;
                }

                model.BoatOwnerID = (int)Session["BoatOwnerID"];
                model.Status      = BoatStatus.Pending;
                model.DateAdded   = DateTime.Now;

                db.Boats.Add(model);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Boat submitted successfully! It is now pending TNPA approval.";
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
        public ActionResult EditBoat(Boat model, HttpPostedFileBase imageFile, HttpPostedFileBase imageFile2, HttpPostedFileBase imageFile3)
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

            if (ModelState.IsValid)
            {
                string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
                string uploadsFolder = Server.MapPath("~/Content/BoatImages/");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                // Main image
                if (imageFile != null && imageFile.ContentLength > 0)
                {
                    string ext = Path.GetExtension(imageFile.FileName).ToLower();
                    if (!Array.Exists(allowedExtensions, e => e == ext))
                    {
                        ModelState.AddModelError("ImagePath", "Only JPG, PNG or GIF images are allowed.");
                        return View(model);
                    }
                    string fileName = Guid.NewGuid().ToString() + ext;
                    imageFile.SaveAs(Path.Combine(uploadsFolder, fileName));
                    boat.ImagePath = "~/Content/BoatImages/" + fileName;
                }

                // Side view image
                if (imageFile2 != null && imageFile2.ContentLength > 0)
                {
                    string ext = Path.GetExtension(imageFile2.FileName).ToLower();
                    if (!Array.Exists(allowedExtensions, e => e == ext))
                    {
                        ModelState.AddModelError("ImagePath2", "Only JPG, PNG or GIF images are allowed.");
                        return View(model);
                    }
                    string fileName = Guid.NewGuid().ToString() + ext;
                    imageFile2.SaveAs(Path.Combine(uploadsFolder, fileName));
                    boat.ImagePath2 = "~/Content/BoatImages/" + fileName;
                }

                // Interior image
                if (imageFile3 != null && imageFile3.ContentLength > 0)
                {
                    string ext = Path.GetExtension(imageFile3.FileName).ToLower();
                    if (!Array.Exists(allowedExtensions, e => e == ext))
                    {
                        ModelState.AddModelError("ImagePath3", "Only JPG, PNG or GIF images are allowed.");
                        return View(model);
                    }
                    string fileName = Guid.NewGuid().ToString() + ext;
                    imageFile3.SaveAs(Path.Combine(uploadsFolder, fileName));
                    boat.ImagePath3 = "~/Content/BoatImages/" + fileName;
                }

                // Update fields — if critical info changed, reset to Pending
                bool requiresReview = (boat.RegistrationNumber != model.RegistrationNumber ||
                                       boat.BoatName != model.BoatName);

                boat.BoatName            = model.BoatName;
                boat.RegistrationNumber  = model.RegistrationNumber;
                boat.BoatType            = model.BoatType;
                boat.Description         = model.Description;
                boat.MaxPassengers       = model.MaxPassengers;
                boat.PricePerTrip        = model.PricePerTrip;
                boat.HarbourLocation     = model.HarbourLocation;
                boat.LifeJacketQuantity  = model.LifeJacketQuantity;
                boat.HasFishingEquipment = model.HasFishingEquipment;
                boat.HasDecoration       = model.HasDecoration;
                boat.HasSoundSystem      = model.HasSoundSystem;

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
        // BOOKINGS (view bookings for owner's boats)
        // ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult Bookings()
        {
            if (Session["BoatOwnerID"] == null)
                return RedirectToAction("Login");

            int ownerID = (int)Session["BoatOwnerID"];
            var bookings = db.Bookings
                .Where(b => b.Boat.BoatOwnerID == ownerID)
                .OrderByDescending(b => b.BookingDate)
                .ToList();

            return View(bookings);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}
