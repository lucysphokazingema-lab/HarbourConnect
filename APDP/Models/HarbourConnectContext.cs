using System.Data.Entity;

namespace APDP.Models
{
    public class HarbourConnectContext : DbContext
    {
        public HarbourConnectContext()
            : base("HarbourConnectDB")
        {
        }

        public DbSet<BoatOwner> BoatOwners { get; set; }
        public DbSet<Boat>      Boats      { get; set; }
        public DbSet<TnpaAdmin> TnpaAdmins { get; set; }
        public DbSet<Customer>  Customers  { get; set; }
        public DbSet<Booking>   Bookings   { get; set; }
        public DbSet<Driver>    Drivers    { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Decimal precision — EF6 way
            modelBuilder.Entity<Boat>()
                        .Property(b => b.PricePerTrip)
                        .HasPrecision(18, 2);

            modelBuilder.Entity<Booking>()
                        .Property(b => b.TotalPrice)
                        .HasPrecision(18, 2);

            // Driver → AssignedBoat: no cascade delete to avoid multiple
            // cascade paths
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

            base.OnModelCreating(modelBuilder);
        }
    }
}
