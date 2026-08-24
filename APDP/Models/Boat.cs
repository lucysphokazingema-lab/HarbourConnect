using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APDP.Models
{
    public enum BoatStatus
    {
        Pending,
        Approved,
        Rejected
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

        [Required(ErrorMessage = "Price per trip is required.")]
        [DataType(DataType.Currency)]
        [Display(Name = "Price per Trip (R)")]
        public decimal PricePerTrip { get; set; }

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

        // ── Safety Equipment ────────────────────────────────────────
        [Required(ErrorMessage = "Life jacket quantity is required.")]
        [Range(0, 500)]
        [Display(Name = "Life Jacket Quantity")]
        public int LifeJacketQuantity { get; set; }

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

        [Display(Name = "Rejection Reason")]
        [StringLength(500)]
        public string RejectionReason { get; set; }

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
        public virtual ICollection<Booking> Bookings { get; set; }
        public virtual ICollection<BoatRating> Ratings { get; set; }
    }
}
