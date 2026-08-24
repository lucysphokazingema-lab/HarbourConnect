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

        [Required(ErrorMessage = "Number of passengers is required.")]
        [Range(1, 200)]
        [Display(Name = "Number of Passengers")]
        public int NumberOfPassengers { get; set; }

        [Display(Name = "Special Requests")]
        [StringLength(500)]
        public string SpecialRequests { get; set; }

        [Display(Name = "Status")]
        public BookingStatus Status { get; set; }

        public DateTime BookingDate { get; set; }

        [Display(Name = "Total Price (R)")]
        public decimal TotalPrice { get; set; }

        // ── Ride tracking ─────────────────────────────────────────────
        [Display(Name = "Ride Started At")]
        public DateTime? RideStartedAt { get; set; }

        [Display(Name = "Ride Ended At")]
        public DateTime? RideEndedAt { get; set; }

        // Whether a post-ride rating has been submitted for this booking
        [Display(Name = "Has Been Rated")]
        public bool IsRated { get; set; }
    }
}
