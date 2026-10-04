using System;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Linq;
using APDP.Helpers;

namespace APDP.Models
{
    /// <summary>
    /// Brings the database schema up to date on start-up without ever dropping it.
    /// Schema changes that would lose data are refused (AutomaticMigrationDataLossAllowed = false).
    /// </summary>
    public class HarbourConnectInitializer
        : MigrateDatabaseToLatestVersion<HarbourConnectContext, HarbourConnectMigrationsConfiguration>
    {
    }

    public sealed class HarbourConnectMigrationsConfiguration : DbMigrationsConfiguration<HarbourConnectContext>
    {
        public HarbourConnectMigrationsConfiguration()
        {
            AutomaticMigrationsEnabled        = true;
            AutomaticMigrationDataLossAllowed = false;
            ContextKey                        = "APDP.Models.HarbourConnectContext";
        }

        protected override void Seed(HarbourConnectContext context)
        {
            // Demo accounts have well-known passwords, so they are only created
            // when SeedDemoData is explicitly "true" (local development).
            if (string.Equals(ConfigurationManager.AppSettings["SeedDemoData"], "true", StringComparison.OrdinalIgnoreCase))
                SeedDemoData(context);

            SeedInitialAdmin(context);
        }

        /// <summary>
        /// Creates the first TNPA admin from InitialAdminEmail / InitialAdminPassword
        /// (set as Azure App Settings / Key Vault references) when no admin exists yet.
        /// </summary>
        private static void SeedInitialAdmin(HarbourConnectContext context)
        {
            string email    = ConfigurationManager.AppSettings["InitialAdminEmail"];
            string password = ConfigurationManager.AppSettings["InitialAdminPassword"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
            if (context.TnpaAdmins.Any()) return;

            context.TnpaAdmins.Add(new TnpaAdmin
            {
                FullName     = "TNPA Administrator",
                Email        = email,
                PasswordHash = PasswordHasher.Hash(password)
            });
            context.SaveChanges();
        }

        private static void SeedDemoData(HarbourConnectContext context)
        {
            // Idempotent: Seed can run on every start-up.
            if (context.BoatOwners.Any(o => o.Email == "owner@demo.co.za")) return;

            // ── TNPA Admin ──────────────────────────────────────────
            context.TnpaAdmins.Add(new TnpaAdmin
            {
                FullName     = "TNPA Administrator",
                Email        = "admin@tnpa.co.za",
                PasswordHash = PasswordHasher.Hash("Admin@123")
            });

            // ── Boat Owner ──────────────────────────────────────────
            var owner = new BoatOwner
            {
                FullName     = "Demo Boat Owner",
                BusinessName = "Harbour Marine Demo",
                PhoneNumber  = "0712345678",
                Email        = "owner@demo.co.za",
                PasswordHash = PasswordHasher.Hash("Owner@123")
            };
            context.BoatOwners.Add(owner);

            // ── Customer ────────────────────────────────────────────
            context.Customers.Add(new Customer
            {
                FullName       = "Demo Customer",
                PhoneNumber    = "0823456789",
                Email          = "customer@demo.co.za",
                PasswordHash   = PasswordHasher.Hash("Customer@123"),
                DateRegistered = DateTime.Now
            });

            // ── Demo Boat ───────────────────────────────────────────
            var demoBoat = new Boat
            {
                BoatName                = "Sea Breeze",
                RegistrationNumber      = "SA-DRB-0001",
                BoatType                = "Leisure Cruiser",
                Description             = "A comfortable leisure cruiser perfect for harbour tours and sunset trips.",
                MaxPassengers           = 12,
                PriceAdult              = 350.00m,
                PriceChild              = 175.00m,
                HarbourLocation         = "Durban Harbour",
                HasLifeJackets          = true,
                HasMedKit               = true,
                HasFireExtinguisher     = true,
                HasFishingEquipment     = false,
                HasDecoration           = true,
                HasSoundSystem          = true,
                DisabilityAccommodation = "Wide boarding platform and seating available for mobility-impaired passengers.",
                Status                  = BoatStatus.Active,   // approved by TNPA and fee paid
                CertificateNumber       = "HC-DEMO-00001",
                RegistrationPaidDate    = DateTime.Now,
                RegistrationPaymentReference = "DEMO-SEED",
                DateAdded               = DateTime.Now,
                AverageRating              = 0,
                TotalRatings               = 0,
                DefaultTripDurationMinutes = 60,
                MaxBookingHours            = 4,
                BufferMinutes              = 30,
                OperatingStartHour         = 8,
                OperatingEndHour           = 16,
                BoatOwner                  = owner
            };
            context.Boats.Add(demoBoat);

            // ── Driver ──────────────────────────────────────────────
            context.Drivers.Add(new Driver
            {
                FullName       = "Demo Driver",
                Email          = "driver@demo.co.za",
                PhoneNumber    = "0734567890",
                PasswordHash   = PasswordHasher.Hash("Driver@123"),
                LicenseNumber  = "DRV-2024-001",
                Status         = DriverStatus.Active,
                DateRegistered = DateTime.Now,
                BoatOwner      = owner,
                AssignedBoat   = demoBoat
            });

            context.SaveChanges();

            // Weekly timetable: Mon–Fri 08:00–16:00, Sat 09:00–13:00, Sun closed
            BoatAvailability.SaveWeek(context, demoBoat, BoatAvailability.DefaultWeek());
            context.SaveChanges();
        }
    }
}
