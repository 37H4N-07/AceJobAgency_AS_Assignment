using System.ComponentModel.DataAnnotations;

namespace AceJobAgency_AS_Assignment.Models
{
    public class PasswordReset
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string ResetToken { get; set; } = string.Empty;

        public DateTime RequestedAt { get; set; }

        public DateTime ExpiresAt { get; set; }

        public bool IsUsed { get; set; } = false;

        public string? IpAddress { get; set; }
    }
}