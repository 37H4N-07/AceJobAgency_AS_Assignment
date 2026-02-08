using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AceJobAgency_AS_Assignment.Models;
using AceJobAgency_AS_Assignment.Data;
using AceJobAgency_AS_Assignment.Services;
using System.ComponentModel.DataAnnotations;

namespace AceJobAgency_AS_Assignment.Pages
{
    public class VerifyCodeModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly AuthDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ICodeGeneratorService _codeGenerator;

        [BindProperty(SupportsGet = true)]
        public string Email { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string CodeType { get; set; } = string.Empty;

        [BindProperty]
        public string Code { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public VerifyCodeModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            AuthDbContext context,
            IEmailService emailService,
            ICodeGeneratorService codeGenerator)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _emailService = emailService;
            _codeGenerator = codeGenerator;
        }

        public void OnGet()
        {
            Email = TempData["Email"]?.ToString() ?? Email;
            CodeType = TempData["CodeType"]?.ToString() ?? CodeType;

            // Keep TempData for subsequent requests
            TempData.Keep("Email");
            TempData.Keep("CodeType");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Manual validation - only validate when form is submitted
            if (string.IsNullOrWhiteSpace(Code))
            {
                ErrorMessage = "Verification code is required.";
                return Page();
            }

            if (Code.Length != 6)
            {
                ErrorMessage = "Code must be 6 digits.";
                return Page();
            }

            var verificationType = Enum.Parse<CodeType>(CodeType);

            var verificationCode = await _context.EmailVerificationCodes
                .Where(v => v.Email == Email &&
                           v.Code == Code &&
                           v.Type == verificationType &&
                           !v.IsUsed)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefaultAsync();

            if (verificationCode == null)
            {
                ErrorMessage = "Invalid verification code. Please check your email and try again.";
                await LogAuditAsync(Email, $"{CodeType} Verification Failed", "Invalid code");
                return Page();
            }

            if (verificationCode.ExpiresAt < DateTime.UtcNow)
            {
                ErrorMessage = "This verification code has expired. Please request a new one.";
                await LogAuditAsync(Email, $"{CodeType} Verification Failed", "Code expired");
                return Page();
            }

            // Mark code as used
            verificationCode.IsUsed = true;
            verificationCode.UsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var user = await _userManager.FindByEmailAsync(Email);
            if (user == null)
            {
                ErrorMessage = "User not found.";
                return Page();
            }

            if (CodeType == "Registration")
            {
                user.IsEmailVerified = true;
                user.EmailVerifiedAt = DateTime.UtcNow;
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);

                await LogAuditAsync(user.Id, "Email Verified", "Registration email verified successfully");

                SuccessMessage = "Email verified successfully! You can now login to your account.";
                return Page();
            }
            else if (CodeType == "Login2FA")
            {
                await _signInManager.SignInAsync(user, isPersistent: false);

                var sessionId = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("UserEmail", user.Email!);
                HttpContext.Session.SetString("UserId", user.Id);
                HttpContext.Session.SetString("SessionId", sessionId);

                var loginHistory = new LoginHistory
                {
                    UserId = user.Id,
                    SessionId = sessionId,
                    LoginTime = DateTime.UtcNow,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    IsActive = true
                };
                _context.LoginHistories.Add(loginHistory);
                await _context.SaveChangesAsync();

                await LogAuditAsync(user.Id, "2FA Login Success", "User logged in with 2FA verification");

                return RedirectToPage("/Index");
            }
            else if (CodeType == "PasswordReset")
            {
                TempData["Email"] = Email;
                TempData["VerifiedPasswordReset"] = true; // Flag that code was verified
                return RedirectToPage("/ResetPassword");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostResendAsync(string email, string codeType)
        {
            Email = email;
            CodeType = codeType;

            var user = await _userManager.FindByEmailAsync(Email);
            if (user == null)
            {
                ErrorMessage = "User not found.";
                return Page();
            }

            // Invalidate old codes
            var oldCodes = await _context.EmailVerificationCodes
                .Where(v => v.Email == Email && !v.IsUsed)
                .ToListAsync();

            foreach (var oldCode in oldCodes)
            {
                oldCode.ExpiresAt = DateTime.UtcNow; // Expire immediately
            }

            // Generate new code
            var newCode = _codeGenerator.GenerateCode();
            var verificationType = Enum.Parse<CodeType>(CodeType);

            var verificationCode = new EmailVerificationCode
            {
                Email = Email,
                Code = newCode,
                Type = verificationType,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            };

            _context.EmailVerificationCodes.Add(verificationCode);
            await _context.SaveChangesAsync();

            try
            {
                string purpose = CodeType.ToLower() == "login2fa" ? "login" :
                                CodeType.ToLower() == "registration" ? "registration" :
                                "passwordreset";

                await _emailService.SendVerificationCodeAsync(Email, newCode, purpose);
                SuccessMessage = "A new verification code has been sent to your email. Please check your inbox.";
            }
            catch
            {
                SuccessMessage = $"Failed to send email. Your new code is: <strong>{newCode}</strong>";
            }

            // Keep data in TempData
            TempData["Email"] = Email;
            TempData["CodeType"] = CodeType;

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