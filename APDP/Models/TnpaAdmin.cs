using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APDP.Models
{
    public class TnpaAdmin
    {
        [Key]
        public int TnpaAdminID { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(150)]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required]
        [StringLength(100)]
        [DataType(DataType.Password)]
        [NotMapped]
        public string Password { get; set; }

        // Salted PBKDF2 hash (see Helpers.PasswordHasher); the plain Password is never stored.
        [StringLength(256)]
        public string PasswordHash { get; set; }
    }
}
