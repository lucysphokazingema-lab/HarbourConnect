using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APDP.Models
{
    /// <summary>
    /// One row per weekday: when the boat takes trips on that day.
    /// A boat has seven rows (Sunday..Saturday); see Helpers.BoatAvailability.
    /// </summary>
    public class BoatOpeningHours
    {
        [Key]
        public int BoatOpeningHoursID { get; set; }

        [Required]
        public int BoatID { get; set; }

        [ForeignKey("BoatID")]
        public virtual Boat Boat { get; set; }

        /// <summary>0 = Sunday … 6 = Saturday (same numbering as System.DayOfWeek).</summary>
        [Range(0, 6)]
        public int DayOfWeek { get; set; }

        public bool IsOpen { get; set; }

        /// <summary>Minutes from midnight when the first trip may start.</summary>
        [Range(0, 1440)]
        public int OpenMinutes { get; set; }

        /// <summary>Minutes from midnight by which the last trip must be back.</summary>
        [Range(0, 1440)]
        public int CloseMinutes { get; set; }
    }
}
