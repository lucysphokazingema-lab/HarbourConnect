using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APDP.Models
{
    public enum DriverStatus
    {
        Active,
        Inactive,
        Suspended
    }

    public class Driver
    {
        [Key]
        public int DriverID { get; set; }

        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(150)]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
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

        [Required(ErrorMessage = "License number is required.")]
        [StringLength(50)]
        [Display(Name = "License Number")]
        public string LicenseNumber { get; set; }

        [Display(Name = "Status")]
        public DriverStatus Status { get; set; }

        public DateTime DateRegistered { get; set; }

        // A driver is assigned to a boat owner
        public int? BoatOwnerID { get; set; }

        [ForeignKey("BoatOwnerID")]
        public virtual BoatOwner BoatOwner { get; set; }

        // A driver can be assigned to a specific boat
        public int? AssignedBoatID { get; set; }

        [ForeignKey("AssignedBoatID")]
        public virtual Boat AssignedBoat { get; set; }
    }
}
