using System;
using System.Data.Entity;

namespace APDP.Models
{
    /// <summary>
    /// Drops and recreates the database whenever the model changes.
    /// Perfect for development — never use DropCreateDatabaseAlways in production.
    /// </summary>
    public class HarbourConnectInitializer : DropCreateDatabaseIfModelChanges<HarbourConnectContext>
    {
        protected override void Seed(HarbourConnectContext context)
        {
            // ── TNPA Admin ─────────────────────────────────────────────
            // Login: admin@tnpa.co.za  /  Admin@123
            context.TnpaAdmins.Add(new TnpaAdmin
            {
                FullName = "TNPA Administrator",
                Email    = "admin@tnpa.co.za",
                Password = "Admin@123"
            });

            // ── Boat Owner ─────────────────────────────────────────────
            // Login: owner@demo.co.za  /  Owner@123
            var owner = new BoatOwner
            {
                FullName     = "Demo Boat Owner",
                BusinessName = "Harbour Marine Demo",
                PhoneNumber  = "0712345678",
                Email        = "owner@demo.co.za",
                Password     = "Owner@123"
            };
            context.BoatOwners.Add(owner);

            // ── Customer ───────────────────────────────────────────────
            // Login: customer@demo.co.za  /  Customer@123
            context.Customers.Add(new Customer
            {
                FullName       = "Demo Customer",
                PhoneNumber    = "0823456789",
                Email          = "customer@demo.co.za",
                Password       = "Customer@123",
                DateRegistered = DateTime.Now
            });

            // ── Driver ─────────────────────────────────────────────────
            // Login: driver@demo.co.za  /  Driver@123
            context.Drivers.Add(new Driver
            {
                FullName       = "Demo Driver",
                Email          = "driver@demo.co.za",
                PhoneNumber    = "0734567890",
                Password       = "Driver@123",
                LicenseNumber  = "DRV-2024-001",
                Status         = DriverStatus.Active,
                DateRegistered = DateTime.Now,
                BoatOwner      = owner
            });

            context.SaveChanges();
        }
    }
}
