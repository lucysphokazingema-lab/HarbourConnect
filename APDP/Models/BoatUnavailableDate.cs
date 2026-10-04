using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APDP.Models
{
    /// <summary>
    /// Dates on which a boat is marked unavailable by the boat owner.
    /// No bookings can be made for these dates.
    /// </summary>
    public class BoatUnavailableDate
    {
        [Key]
        public int UnavailableDateID { get; set; }

        [Required]
        public int BoatID { get; set; }

        [ForeignKey("BoatID")]
        public virtual Boat Boat { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Unavailable Date")]
        public DateTime Date { get; set; }

        [StringLength(200)]
        [Display(Name = "Reason (optional)")]
        public string Reason { get; set; }
    }
}
