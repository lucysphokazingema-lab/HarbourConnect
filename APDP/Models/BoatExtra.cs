using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APDP.Models
{
    /// <summary>
    /// An optional add-on that a boat owner can offer on their boat,
    /// e.g. "Champagne Package" at R250 per person or "Fishing Pack" at R150.
    /// Customers see these on the booking form and can tick whichever they want;
    /// the price is added to the booking total automatically.
    /// </summary>
    public class BoatExtra
    {
        [Key]
        public int ExtraID { get; set; }

        [Required]
        public int BoatID { get; set; }

        [ForeignKey("BoatID")]
        public virtual Boat Boat { get; set; }

        [Required(ErrorMessage = "Extra name is required.")]
        [StringLength(100)]
        [Display(Name = "Extra Name")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Price is required.")]
        [Display(Name = "Price (R)")]
        public decimal Price { get; set; }
    }
}
