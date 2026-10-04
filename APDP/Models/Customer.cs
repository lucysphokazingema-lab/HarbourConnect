using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APDP.Models
{
    public class Customer
    {
        [Key]
        public int CustomerID { get; set; }

        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(100, MinimumLength = 2)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address e.g. name@example.com.")]
        [StringLength(150)]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^\+?[0-9\s\-]{7,20}$",
            ErrorMessage = "Enter a valid phone number including country code e.g. +27821234567.")]
        [StringLength(20)]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        [DataType(DataType.Password)]
        [NotMapped]
        public string Password { get; set; }

        // Salted PBKDF2 hash (see Helpers.PasswordHasher); the plain Password is never stored.
        [StringLength(256)]
        public string PasswordHash { get; set; }

        public DateTime DateRegistered { get; set; }

        // Navigation
        public virtual ICollection<Booking> Bookings { get; set; }
    }
}
