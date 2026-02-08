using System.ComponentModel.DataAnnotations;

namespace AceJobAgency_AS_Assignment.Models
{
    public enum CodeType
    {
        Registration,
        Login2FA,
        PasswordReset
    }

    public class EmailVerificationCode
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Code { get; set; } = string.Empty;  // 6-digit code

        [Required]
        public CodeType Type { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ExpiresAt { get; set; }  // Valid for 10 minutes

        public bool IsUsed { get; set; } = false;

        public DateTime? UsedAt { get; set; }

        public string? IpAddress { get; set; }
    }
}