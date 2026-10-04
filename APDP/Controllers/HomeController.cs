using System.Web.Mvc;

namespace APDP.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            // Logged-in users belong in their own portal, not the public landing page
            if (Session["CustomerID"] != null)  return RedirectToAction("Dashboard", "Customer");
            if (Session["BoatOwnerID"] != null) return RedirectToAction("Dashboard", "BoatOwner");
            if (Session["TnpaAdminID"] != null) return RedirectToAction("Dashboard", "Tnpa");
            if (Session["DriverID"] != null)    return RedirectToAction("Dashboard", "Driver");

            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "HarbourConnect – connecting boat owners, customers, and the harbour authority.";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Get in touch with us.";
            return View();
        }
    }
}
