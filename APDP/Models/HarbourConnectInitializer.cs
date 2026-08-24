using System;
using System.Data.Entity;

namespace APDP.Models
{
    /// <summary>
    /// Drops and recreates the database whenever the model changes.
    /// </summary>
    public class HarbourConnectInitializer : DropCreateDatabaseIfModelChanges<HarbourConnectContext>
    {
        protected override void Seed(HarbourConnectContext context)
        {
            // ── TNPA Admin ──────────────────────────────────────────
            context.TnpaAdmins.Add(new TnpaAdmin
            {
                FullName = "TNPA Administrator",
                Email    = "admin@tnpa.co.za",
                Password = "Admin@123"
            });

            // ── Boat Owner ──────────────────────────────────────────
            var owner = new BoatOwner
            {
                FullName     = "Demo Boat Owner",
                BusinessName = "Harbour Marine Demo",
                PhoneNumber  = "0712345678",
                Email        = "owner@demo.co.za",
                Password     = "Owner@123"
            };
            context.BoatOwners.Add(owner);

            // ── Customer ────────────────────────────────────────────
            context.Customers.Add(new Customer
            {
                FullName       = "Demo Customer",
                PhoneNumber    = "0823456789",
                Email          = "customer@demo.co.za",
                Password       = "Customer@123",
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
                PricePerTrip            = 1500.00m,
                HarbourLocation         = "Durban Harbour",
                LifeJacketQuantity      = 14,
                HasMedKit               = true,
                HasFireExtinguisher     = true,
                HasFishingEquipment     = false,
                HasDecoration           = true,
                HasSoundSystem          = true,
                DisabilityAccommodation = "Wide boarding platform and seating available for mobility-impaired passengers.",
                Status                  = BoatStatus.Approved,
                DateAdded               = DateTime.Now,
                AverageRating           = 0,
                TotalRatings            = 0,
                BoatOwner               = owner
            };
            context.Boats.Add(demoBoat);

            // ── Driver ──────────────────────────────────────────────
            context.Drivers.Add(new Driver
            {
                FullName       = "Demo Driver",
                Email          = "driver@demo.co.za",
                PhoneNumber    = "0734567890",
                Password       = "Driver@123",
                LicenseNumber  = "DRV-2024-001",
                Status         = DriverStatus.Active,
                DateRegistered = DateTime.Now,
                BoatOwner      = owner,
                AssignedBoat   = demoBoat
            });

            context.SaveChanges();
        }
    }
}
