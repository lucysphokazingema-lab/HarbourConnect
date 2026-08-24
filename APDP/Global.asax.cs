using System;
using System.Data.Entity;
using System.Threading;
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
            // Register the EF initializer (drops & recreates DB if model changes)
            Database.SetInitializer(new HarbourConnectInitializer());

            // Initialize the database with retry logic.
            // The "model" lock error happens when SQL Server LocalDB is briefly
            // busy — retrying a few times resolves it without any code changes.
            InitializeDatabaseWithRetry(maxAttempts: 5, delayMs: 1500);

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        private static void InitializeDatabaseWithRetry(int maxAttempts, int delayMs)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using (var db = new HarbourConnectContext())
                    {
                        db.Database.Initialize(force: false);
                    }
                    return; // success — exit the loop
                }
                catch (Exception ex) when (attempt < maxAttempts &&
                    (ex.Message.Contains("exclusive lock") ||
                     ex.Message.Contains("CREATE DATABASE") ||
                     ex.Message.Contains("model")))
                {
                    // Brief pause then retry
                    Thread.Sleep(delayMs);
                }
                // On the final attempt let the exception bubble up naturally
            }

            // Final attempt outside the loop so any exception is unhandled
            // and shows the real error if all retries failed
            using (var db = new HarbourConnectContext())
            {
                db.Database.Initialize(force: false);
            }
        }
    }
}
