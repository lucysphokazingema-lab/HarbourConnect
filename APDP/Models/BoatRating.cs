using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APDP.Models
{
    public class BoatRating
    {
        [Key]
        public int RatingID { get; set; }

        [Required]
        public int BoatID { get; set; }

        [ForeignKey("BoatID")]
        public virtual Boat Boat { get; set; }

        [Required]
        public int CustomerID { get; set; }

        [ForeignKey("CustomerID")]
        public virtual Customer Customer { get; set; }

        // The booking this rating is linked to
        public int? BookingID { get; set; }

        [ForeignKey("BookingID")]
        public virtual Booking Booking { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        [Display(Name = "Rating (1–5 stars)")]
        public int Stars { get; set; }

        [StringLength(500)]
        [Display(Name = "Comment")]
        public string Comment { get; set; }

        public DateTime DateRated { get; set; }
    }
}
