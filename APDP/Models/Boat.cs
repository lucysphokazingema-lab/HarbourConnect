using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APDP.Models
{
    public enum BoatStatus
    {
        Pending,
        AwaitingPayment,
        Active,
        Approved,
        Rejected,
        Inactive,
        Removed      // taken down by TNPA; kept (not deleted) so booking history survives
    }

    public class Boat
    {
        [Key]
        public int BoatID { get; set; }

        [Required(ErrorMessage = "Boat name is required.")]
        [StringLength(100)]
        [Display(Name = "Boat Name")]
        public string BoatName { get; set; }

        [Required(ErrorMessage = "Registration number is required.")]
        [StringLength(50)]
        [RegularExpression(@"^[A-Za-z0-9\-]{3,50}$",
            ErrorMessage = "Registration number may only contain letters, numbers and hyphens (e.g. SA-DBN-1234).")]
        [Display(Name = "Registration Number")]
        public string RegistrationNumber { get; set; }

        [Required(ErrorMessage = "Boat type is required.")]
        [StringLength(50)]
        [Display(Name = "Boat Type")]
        public string BoatType { get; set; }

        [StringLength(500)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Maximum passengers is required.")]
        [Range(1, 200, ErrorMessage = "Must be between 1 and 200.")]
        [Display(Name = "Maximum Passengers")]
        public int MaxPassengers { get; set; }

        // Pricing: per person per hour (adult/child)
        [Required(ErrorMessage = "Adult hourly rate is required.")]
        [DataType(DataType.Currency)]
        [Display(Name = "Adult Hourly Rate (R/hr)")]
        public decimal PriceAdult { get; set; }

        [Required(ErrorMessage = "Child hourly rate is required.")]
        [DataType(DataType.Currency)]
        [Display(Name = "Child Hourly Rate (R/hr)")]
        public decimal PriceChild { get; set; }

        [Required(ErrorMessage = "Harbour location is required.")]
        [StringLength(100)]
        [Display(Name = "Harbour Location")]
        public string HarbourLocation { get; set; } 

        [Display(Name = "Boat Image")]
        [StringLength(255)]
        public string ImagePath { get; set; }

        [Display(Name = "Side View Image")]
        [StringLength(255)]
        public string ImagePath2 { get; set; }

        [Display(Name = "Interior Image")]
        [StringLength(255)]
        public string ImagePath3 { get; set; }

        [Display(Name = "Additional Photo 4")]
        [StringLength(255)]
        public string ImagePath4 { get; set; }

        [Display(Name = "Additional Photo 5")]
        [StringLength(255)]
        public string ImagePath5 { get; set; }

        [Display(Name = "Additional Photo 6")]
        [StringLength(255)]
        public string ImagePath6 { get; set; }

        // ── Videos ──────────────────────────────────────────────────
        [Display(Name = "Video 1")]
        [StringLength(255)]
        public string VideoPath1 { get; set; }

        [Display(Name = "Video 2")]
        [StringLength(255)]
        public string VideoPath2 { get; set; }

        [Display(Name = "Video 3")]
        [StringLength(255)]
        public string VideoPath3 { get; set; }

        // ── Safety Equipment ────────────────────────────────────────
        // Life jackets are now a boolean checkbox (presence). Photos still stored separately.
        [Display(Name = "Life Jackets On Board")]
        public bool HasLifeJackets { get; set; }

        [StringLength(255)]
        [Display(Name = "Life Jacket Photo")]
        public string LifeJacketImagePath { get; set; }

        [Display(Name = "Medical Kit On Board")]
        public bool HasMedKit { get; set; }

        [StringLength(255)]
        [Display(Name = "Medical Kit Photo")]
        public string MedKitImagePath { get; set; }

        [Display(Name = "Fire Extinguisher On Board")]
        public bool HasFireExtinguisher { get; set; }

        [StringLength(255)]
        [Display(Name = "Fire Extinguisher Photo")]
        public string FireExtImagePath { get; set; }

        // ── Features & Extras ────────────────────────────────────────
        [Display(Name = "Fishing Equipment Available")]
        public bool HasFishingEquipment { get; set; }

        [StringLength(255)]
        [Display(Name = "Fishing Equipment Photo")]
        public string FishingImagePath { get; set; }

        [Display(Name = "Decoration Available")]
        public bool HasDecoration { get; set; }

        [StringLength(255)]
        [Display(Name = "Decoration Photo")]
        public string DecorationImagePath { get; set; }

        [Display(Name = "Sound System Available")]
        public bool HasSoundSystem { get; set; }

        [StringLength(255)]
        [Display(Name = "Sound System Photo")]
        public string SoundSystemImagePath { get; set; }

        // ── Disability Accommodation ─────────────────────────────────
        [Display(Name = "This boat accommodates people with disabilities")]
        public bool IsDisabilityFriendly { get; set; }

        [StringLength(300)]
        [Display(Name = "Disability Accommodation")]
        public string DisabilityAccommodation { get; set; }

        // ── Supporting Documents ─────────────────────────────────
        [StringLength(255)]
        [Display(Name = "Owner ID Document")]
        public string OwnerIdDocumentPath { get; set; }

        [StringLength(255)]
        [Display(Name = "Boat Registration Certificate")]
        public string BoatRegistrationCertPath { get; set; }

        [StringLength(255)]
        [Display(Name = "Boat Licence / Operating Licence")]
        public string BoatLicencePath { get; set; }

        // ── Status & Admin ───────────────────────────────────────────
        [Display(Name = "Status")]
        public BoatStatus Status { get; set; }

        // Also holds the TNPA removal reason when Status == Removed
        [Display(Name = "Rejection Reason")]
        [StringLength(500)]
        public string RejectionReason { get; set; }

        // ── Registration Certificate ─────────────────────────────────
        [Display(Name = "Certificate Number")]
        [StringLength(50)]
        public string CertificateNumber { get; set; }

        [Display(Name = "Registration Fee Paid Date")]
        public DateTime? RegistrationPaidDate { get; set; }

        [StringLength(50)]
        [Display(Name = "Registration Payment Reference")]
        public string RegistrationPaymentReference { get; set; }

        // ── Operating Hours ──────────────────────────────────────────
        // Legacy single daily window. The weekly timetable (BoatOpeningHours) now drives
        // availability; these are kept in step by BoatAvailability.SaveWeek and used only
        // for boats created before timetables existed.
        [Range(0, 24)]
        [Display(Name = "Operating Start Hour")]
        public int OperatingStartHour { get; set; }

        [Range(0, 24)]
        [Display(Name = "Operating End Hour")]
        public int OperatingEndHour { get; set; }

        // ── Trip Duration ────────────────────────────────────────────
        [Display(Name = "Minimum Booking Duration (minutes)")]
        public int DefaultTripDurationMinutes { get; set; }

        /// <summary>Maximum hours a customer may book in a single session (1–9, capped at harbour close).</summary>
        [Display(Name = "Maximum Booking Hours")]
        public int MaxBookingHours { get; set; }

        /// <summary>Turnaround time kept free after each trip (refuel / clean).</summary>
        [Range(0, 120)]
        [Display(Name = "Break Between Trips (minutes)")]
        public int BufferMinutes { get; set; }

        public DateTime DateAdded { get; set; }

        // ── Ratings (denormalised average stored for fast reads) ──────
        [Display(Name = "Average Rating")]
        public double AverageRating { get; set; }

        [Display(Name = "Total Ratings")]
        public int TotalRatings { get; set; }

        // Foreign key
        [Required]
        public int BoatOwnerID { get; set; }

        [ForeignKey("BoatOwnerID")]
        public virtual BoatOwner BoatOwner { get; set; }

        // Navigation
        public virtual ICollection<Booking>   Bookings { get; set; }
        public virtual ICollection<BoatRating> Ratings { get; set; }
        public virtual ICollection<BoatExtra>  Extras  { get; set; }
    }
}
