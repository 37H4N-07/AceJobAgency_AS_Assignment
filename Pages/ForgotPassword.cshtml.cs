using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using AceJobAgency_AS_Assignment.Models;
using AceJobAgency_AS_Assignment.Data;
using AceJobAgency_AS_Assignment.Services;
using System.ComponentModel.DataAnnotations;

namespace AceJobAgency_AS_Assignment.Pages
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuthDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ICodeGeneratorService _codeGenerator;

        [BindProperty]
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public ForgotPasswordModel(
            UserManager<ApplicationUser> userManager,
            AuthDbContext context,
            IEmailService emailService,
            ICodeGeneratorService codeGenerator)
        {
            _userManager = userManager;
            _context = context;
            _emailService = emailService;
            _codeGenerator = codeGenerator;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Email);

            if (user == null)
            {
                SuccessMessage = "If an account with that email exists, we've sent a password reset code.";
                return Page();
            }

            var code = _codeGenerator.GenerateCode();

            var verificationCode = new EmailVerificationCode
            {
                Email = user.Email!,
                Code = code,
                Type = CodeType.PasswordReset,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            };

            _context.EmailVerificationCodes.Add(verificationCode);
            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendVerificationCodeAsync(user.Email!, code, "passwordreset");
                SuccessMessage = "A password reset code has been sent to your email.";
            }
            catch
            {
                SuccessMessage = $"Failed to send email. Your reset code is: <strong>{code}</strong>";
            }

            await LogAuditAsync(user.Id, "Password Reset Requested", "Reset code generated");

            TempData["Email"] = user.Email;
            TempData["CodeType"] = "PasswordReset";

            return Page();
        }

        private async Task LogAuditAsync(string userId, string action, string details)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = action,
                Timestamp = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers["User-Agent"].ToString()
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }
    }
}