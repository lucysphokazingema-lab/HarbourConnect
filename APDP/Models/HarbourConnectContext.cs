using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Linq;

namespace APDP.Models
{
    public class HarbourConnectContext : DbContext
    {
        public HarbourConnectContext()
            : base("HarbourConnectDB")
        {
            // Increase command timeout to 120 seconds (default is 30)
            // Prevents timeout errors when LocalDB is slow to respond
            this.Database.CommandTimeout = 120;
        }

        public DbSet<BoatOwner>           BoatOwners           { get; set; }
        public DbSet<Boat>                Boats                { get; set; }
        public DbSet<BoatExtra>           BoatExtras           { get; set; }
        public DbSet<BoatUnavailableDate> BoatUnavailableDates { get; set; }
        public DbSet<BoatOpeningHours>    BoatOpeningHours     { get; set; }
        public DbSet<TnpaAdmin>           TnpaAdmins           { get; set; }
        public DbSet<Customer>            Customers            { get; set; }
        public DbSet<Booking>             Bookings             { get; set; }
        public DbSet<Driver>              Drivers              { get; set; }
        public DbSet<BoatRating>          BoatRatings          { get; set; }
        public DbSet<Notification>        Notifications        { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Decimal precision — EF6 way
            modelBuilder.Entity<Boat>()
                        .Property(b => b.PriceAdult)
                        .HasPrecision(18, 2);
            modelBuilder.Entity<Boat>()
                        .Property(b => b.PriceChild)
                        .HasPrecision(18, 2);

            modelBuilder.Entity<Booking>()
                        .Property(b => b.TotalPrice)
                        .HasPrecision(18, 2);

            modelBuilder.Entity<Booking>()
                        .Property(b => b.ExtrasTotal)
                        .HasPrecision(18, 2);

            // TimeSpan is not numeric in EF6 SQL; keep it on the CLR model only.
            modelBuilder.Entity<Booking>().Ignore(b => b.TripStartTime);

            // Driver → AssignedBoat: no cascade delete to avoid multiple cascade paths
            modelBuilder.Entity<Driver>()
                        .HasOptional(d => d.AssignedBoat)
                        .WithMany()
                        .HasForeignKey(d => d.AssignedBoatID)
                        .WillCascadeOnDelete(false);

            modelBuilder.Entity<Driver>()
                        .HasOptional(d => d.BoatOwner)
                        .WithMany()
                        .HasForeignKey(d => d.BoatOwnerID)
                        .WillCascadeOnDelete(false);

            // BoatRating → Customer: no cascade delete to avoid multiple cascade paths
            modelBuilder.Entity<BoatRating>()
                        .HasRequired(r => r.Customer)
                        .WithMany()
                        .HasForeignKey(r => r.CustomerID)
                        .WillCascadeOnDelete(false);

            // BoatRating → Booking: optional, no cascade delete
            modelBuilder.Entity<BoatRating>()
                        .HasOptional(r => r.Booking)
                        .WithMany()
                        .HasForeignKey(r => r.BookingID)
                        .WillCascadeOnDelete(false);

            modelBuilder.Entity<BoatExtra>()
                        .Property(e => e.Price)
                        .HasPrecision(18, 2);

            // BoatExtra → Boat: cascade delete so extras are removed with the boat
            modelBuilder.Entity<BoatExtra>()
                        .HasRequired(e => e.Boat)
                        .WithMany(b => b.Extras)
                        .HasForeignKey(e => e.BoatID)
                        .WillCascadeOnDelete(true);

            // BoatOpeningHours → Boat: cascade delete
            modelBuilder.Entity<BoatOpeningHours>()
                        .HasRequired(h => h.Boat)
                        .WithMany()
                        .HasForeignKey(h => h.BoatID)
                        .WillCascadeOnDelete(true);

            // BoatUnavailableDate → Boat: cascade delete
            modelBuilder.Entity<BoatUnavailableDate>()
                        .HasRequired(u => u.Boat)
                        .WithMany()
                        .HasForeignKey(u => u.BoatID)
                        .WillCascadeOnDelete(true);

            base.OnModelCreating(modelBuilder);
        }

        /// <summary>
        /// Password on the user entities is a [NotMapped] form field; only PasswordHash is stored.
        /// EF still validates unmapped properties on save, so ignore Password here — it is
        /// validated by MVC model binding when a form is posted.
        /// </summary>
        protected override DbEntityValidationResult ValidateEntity(
            DbEntityEntry entityEntry, IDictionary<object, object> items)
        {
            var result = base.ValidateEntity(entityEntry, items);
            if (result.IsValid) return result;

            var errors = result.ValidationErrors.Where(e => e.PropertyName != "Password").ToList();
            return new DbEntityValidationResult(entityEntry, errors);
        }
    }
}
