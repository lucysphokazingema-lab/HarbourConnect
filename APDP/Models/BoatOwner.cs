using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace APDP.Models
{
    public class BoatOwner
    {
        [Key]
        public int BoatOwnerID { get; set; }

        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be at least 2 characters.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Business name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Business name must be at least 2 characters.")]
        [Display(Name = "Business Name")]
        public string BusinessName { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^(\+27|0)[6-8][0-9]{8}$",
            ErrorMessage = "Enter a valid South African number e.g. +27821234567 or 0821234567.")]
        [StringLength(20)]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address e.g. name@example.com.")]
        [StringLength(150)]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        // Strong password: min 8 chars, at least 1 uppercase, 1 lowercase, 1 digit, 1 special char
        [Required(ErrorMessage = "Password is required.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&_#\-])[A-Za-z\d@$!%*?&_#\-]{8,}$",
            ErrorMessage = "Password must be at least 8 characters and include uppercase, lowercase, a number, and a special character (e.g. @$!%*?&).")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        // Navigation
        public virtual ICollection<Boat> Boats { get; set; }
    }
}
