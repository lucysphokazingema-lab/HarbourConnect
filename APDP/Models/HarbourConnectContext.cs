using System.Data.Entity;
using System.Data.Entity.Infrastructure;

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

        public DbSet<BoatOwner>  BoatOwners  { get; set; }
        public DbSet<Boat>       Boats       { get; set; }
        public DbSet<TnpaAdmin>  TnpaAdmins  { get; set; }
        public DbSet<Customer>   Customers   { get; set; }
        public DbSet<Booking>    Bookings    { get; set; }
        public DbSet<Driver>     Drivers     { get; set; }
        public DbSet<BoatRating> BoatRatings { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Decimal precision — EF6 way
            modelBuilder.Entity<Boat>()
                        .Property(b => b.PricePerTrip)
                        .HasPrecision(18, 2);

            modelBuilder.Entity<Booking>()
                        .Property(b => b.TotalPrice)
                        .HasPrecision(18, 2);

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

            base.OnModelCreating(modelBuilder);
        }
    }
}
