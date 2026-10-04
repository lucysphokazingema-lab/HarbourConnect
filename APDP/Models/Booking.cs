using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APDP.Models
{
    public enum BookingStatus
    {
        Pending,
        Confirmed,
        Cancelled,
        Completed
    }

    public class Booking
    {
        [Key]
        public int BookingID { get; set; }

        [Required]
        public int CustomerID { get; set; }

        [ForeignKey("CustomerID")]
        public virtual Customer Customer { get; set; }

        [Required]
        public int BoatID { get; set; }

        [ForeignKey("BoatID")]
        public virtual Boat Boat { get; set; }

        [Required(ErrorMessage = "Trip date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Trip Date")]
        public DateTime TripDate { get; set; }

        /// <summary>
        /// Minutes from midnight. Stored as int because EF6 cannot translate TimeSpan
        /// (DateTime + TimeSpan throws DbArithmeticExpression).
        /// </summary>
        public int TripStartMinutes { get; set; }

        [NotMapped]
        [Display(Name = "Trip Start Time")]
        public TimeSpan TripStartTime
        {
            get { return TimeSpan.FromMinutes(TripStartMinutes); }
            set { TripStartMinutes = (int)Math.Round(value.TotalMinutes); }
        }

        [Required(ErrorMessage = "Number of adult passengers is required.")]
        [Range(0, 200)]
        [Display(Name = "Adult Passengers")]
        public int NumberOfPassengers { get; set; }

        [Range(0, 200)]
        [Display(Name = "Child Passengers")]
        public int ChildPassengers { get; set; }

        [Display(Name = "Special Requests")]
        [StringLength(500)]
        public string SpecialRequests { get; set; }

        [Display(Name = "Status")]
        public BookingStatus Status { get; set; }

        public DateTime BookingDate { get; set; }

        /// <summary>Number of hours booked (e.g. 1, 2, 3, 4). Persisted so it stays correct even if the boat's default duration changes later.</summary>
        [Display(Name = "Trip Duration (hours)")]
        public int TripDurationHours { get; set; }

        [Display(Name = "Total Price (R)")]
        public decimal TotalPrice { get; set; }

        // ── Payment ───────────────────────────────────────────────────
        // A booking is Pending until paid; it holds its slot for BoatAvailability.PaymentHold.
        [Display(Name = "Paid At")]
        public DateTime? PaidAt { get; set; }

        [StringLength(50)]
        [Display(Name = "Payment Reference")]
        public string PaymentReference { get; set; }

        // ── Ride tracking ─────────────────────────────────────────────
        [Display(Name = "Ride Started At")]
        public DateTime? RideStartedAt { get; set; }

        [Display(Name = "Ride Ended At")]
        public DateTime? RideEndedAt { get; set; }

        // Whether a post-ride rating has been submitted for this booking
        [Display(Name = "Has Been Rated")]
        public bool IsRated { get; set; }

        // ── Extras selected at booking time ───────────────────────────
        /// <summary>
        /// Comma-separated list of extra names chosen at booking, e.g. "Champagne Package,Fishing Pack".
        /// Stored as a string so no extra join table is needed.
        /// </summary>
        [Display(Name = "Selected Extras")]
        [StringLength(1000)]
        public string SelectedExtras { get; set; }

        /// <summary>Total price of all selected extras (cached for display on slip).</summary>
        [Display(Name = "Extras Total (R)")]
        public decimal ExtrasTotal { get; set; }
    }
}
