using System.Data.Entity;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using APDP.Models;

namespace APDP
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            // Set the initializer — drops and recreates DB if model changed
            Database.SetInitializer(new HarbourConnectInitializer());

            // Force initialization now so the DB and tables exist before
            // any request comes in
            using (var db = new HarbourConnectContext())
            {
                db.Database.Initialize(force: true);
            }

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
    }
}
