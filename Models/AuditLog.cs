using System.ComponentModel.DataAnnotations;

namespace AceJobAgency_AS_Assignment.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Action { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }
    }
}