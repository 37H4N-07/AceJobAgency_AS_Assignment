using System.ComponentModel.DataAnnotations;

namespace AceJobAgency_AS_Assignment.Models
{
    public class LoginHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string SessionId { get; set; } = string.Empty;

        public DateTime LoginTime { get; set; }

        public DateTime? LogoutTime { get; set; }

        public string? IpAddress { get; set; }

        public bool IsActive { get; set; }
    }
}