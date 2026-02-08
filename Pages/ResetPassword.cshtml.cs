using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AceJobAgency_AS_Assignment.Models;
using AceJobAgency_AS_Assignment.Data;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace AceJobAgency_AS_Assignment.Pages
{
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuthDbContext _context;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

        [BindProperty(SupportsGet = true)]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "New password is required")]
        [MinLength(12, ErrorMessage = "Password must be at least 12 characters")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#])[A-Za-z\d@$!%*?&#]{12,}$",
            ErrorMessage = "Password must contain uppercase, lowercase, number and special character")]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Please confirm your password")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public ResetPasswordModel(
            UserManager<ApplicationUser> userManager,
            AuthDbContext context,
            IPasswordHasher<ApplicationUser> passwordHasher)
        {
            _userManager = userManager;
            _context = context;
            _passwordHasher = passwordHasher;
        }
        public async Task<IActionResult> OnGetAsync()
        {
            Email = TempData["Email"]?.ToString() ?? Email;
            var isVerified = TempData["VerifiedPasswordReset"] as bool?;

            if (string.IsNullOrEmpty(Email) || isVerified != true)
            {
                return RedirectToPage("/ForgotPassword");
            }

            // Keep email in TempData for POST
            TempData.Keep("Email");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Get user by email (don't re-verify code, it was already verified in VerifyCode page)
            var user = await _userManager.FindByEmailAsync(Email);
            if (user == null)
            {
                ErrorMessage = "User not found.";
                return Page();
            }

            // Check password complexity
            int passwordScore = CheckPasswordComplexity(NewPassword);
            if (passwordScore < 5)
            {
                ErrorMessage = "Password does not meet complexity requirements.";
                return Page();
            }

            // Check password history
            if (!string.IsNullOrEmpty(user.PasswordHistory))
            {
                var passwordHistoryList = user.PasswordHistory.Split('|').ToList();

                foreach (var oldPasswordHash in passwordHistoryList)
                {
                    var verificationResult = _passwordHasher.VerifyHashedPassword(user, oldPasswordHash, NewPassword);
                    if (verificationResult == PasswordVerificationResult.Success)
                    {
                        ErrorMessage = "You cannot reuse any of your last 2 passwords.";
                        return Page();
                    }
                }
            }

            // Remove old password
            var removePasswordResult = await _userManager.RemovePasswordAsync(user);
            if (!removePasswordResult.Succeeded)
            {
                ErrorMessage = "Failed to reset password. Please try again.";
                return Page();
            }

            // Add new password
            var addPasswordResult = await _userManager.AddPasswordAsync(user, NewPassword);
            if (!addPasswordResult.Succeeded)
            {
                foreach (var error in addPasswordResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            // Update password history and related fields
            var newPasswordHash = user.PasswordHash!;
            var historyList = string.IsNullOrEmpty(user.PasswordHistory)
                ? new List<string>()
                : user.PasswordHistory.Split('|').ToList();

            historyList.Insert(0, newPasswordHash);

            if (historyList.Count > 2)
            {
                historyList = historyList.Take(2).ToList();
            }

            user.PasswordHistory = string.Join("|", historyList);
            user.PasswordLastChanged = DateTime.UtcNow;
            user.PasswordExpiryDate = DateTime.UtcNow.AddDays(90);
            user.PasswordMinChangeDate = DateTime.UtcNow.AddMinutes(5);

            await _userManager.UpdateAsync(user);

            // Log audit
            await LogAuditAsync(user.Id, "Password Reset", "Password reset successfully");

            SuccessMessage = "Password has been reset successfully! You can now login with your new password.";

            return Page();
        }

        private int CheckPasswordComplexity(string password)
        {
            int score = 0;
            if (password.Length >= 12) score++;
            if (Regex.IsMatch(password, @"[a-z]")) score++;
            if (Regex.IsMatch(password, @"[A-Z]")) score++;
            if (Regex.IsMatch(password, @"[0-9]")) score++;
            if (Regex.IsMatch(password, @"[@$!%*?&]")) score++;
            return score;
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