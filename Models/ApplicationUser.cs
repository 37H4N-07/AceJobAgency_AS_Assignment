using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace AceJobAgency_AS_Assignment.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public string Gender { get; set; } = string.Empty;

        [Required]
        public string NRIC { get; set; } = string.Empty; // Will be encrypted

        [Required]
        public DateTime DateOfBirth { get; set; }

        [MaxLength(500)]
        public string? ResumePath { get; set; }

        [MaxLength(2000)]
        public string? WhoAmI { get; set; }

        // Password History (stores last 2 password hashes separated by |)
        public string? PasswordHistory { get; set; }

        // Password Age Management
        public DateTime? PasswordLastChanged { get; set; }
        public DateTime? PasswordExpiryDate { get; set; }
        public DateTime? PasswordMinChangeDate { get; set; }

        // Email Verification
        public bool IsEmailVerified { get; set; } = false;
        public DateTime? EmailVerifiedAt { get; set; }
    }
}