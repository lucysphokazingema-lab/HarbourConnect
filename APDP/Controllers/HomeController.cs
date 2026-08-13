using System.Web.Mvc;

namespace APDP.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
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
