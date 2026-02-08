using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using AceJobAgency_AS_Assignment.Models;
using AceJobAgency_AS_Assignment.Data;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace AceJobAgency_AS_Assignment.Pages
{
    [Authorize]
    public class ChangePasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly AuthDbContext _context;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

        [BindProperty]
        [Required(ErrorMessage = "Current password is required")]
        public string CurrentPassword { get; set; } = string.Empty;

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

        public ChangePasswordModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            AuthDbContext context,
            IPasswordHasher<ApplicationUser> passwordHasher)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _passwordHasher = passwordHasher;
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

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            if (user.PasswordMinChangeDate.HasValue && DateTime.UtcNow < user.PasswordMinChangeDate.Value)
            {
                var remainingTime = user.PasswordMinChangeDate.Value - DateTime.UtcNow;
                ErrorMessage = $"You must wait {remainingTime.Minutes} more minutes before changing your password again.";
                return Page();
            }

            var passwordCheck = await _userManager.CheckPasswordAsync(user, CurrentPassword);
            if (!passwordCheck)
            {
                ErrorMessage = "Current password is incorrect.";
                await LogAuditAsync(user.Id, "Failed Password Change", "Incorrect current password");
                return Page();
            }

            int passwordScore = CheckPasswordComplexity(NewPassword);
            if (passwordScore < 5)
            {
                ErrorMessage = "New password does not meet complexity requirements.";
                return Page();
            }

            if (!string.IsNullOrEmpty(user.PasswordHistory))
            {
                var passwordHistoryList = user.PasswordHistory.Split('|').ToList();

                foreach (var oldPasswordHash in passwordHistoryList)
                {
                    var verificationResult = _passwordHasher.VerifyHashedPassword(user, oldPasswordHash, NewPassword);
                    if (verificationResult == PasswordVerificationResult.Success)
                    {
                        ErrorMessage = "You cannot reuse any of your last 2 passwords.";
                        await LogAuditAsync(user.Id, "Failed Password Change", "Password reuse attempt");
                        return Page();
                    }
                }
            }

            var result = await _userManager.ChangePasswordAsync(user, CurrentPassword, NewPassword);

            if (result.Succeeded)
            {
                var newPasswordHash = _passwordHasher.HashPassword(user, NewPassword);
                var historyList = string.IsNullOrEmpty(user.PasswordHistory)
                    ? new List<string>()
                    : user.PasswordHistory.Split('|').ToList();

                historyList.Insert(0, user.PasswordHash!);

                if (historyList.Count > 2)
                {
                    historyList = historyList.Take(2).ToList();
                }

                user.PasswordHistory = string.Join("|", historyList);
                user.PasswordLastChanged = DateTime.UtcNow;
                user.PasswordExpiryDate = DateTime.UtcNow.AddDays(90);
                user.PasswordMinChangeDate = DateTime.UtcNow.AddMinutes(5);

                await _userManager.UpdateAsync(user);
                await LogAuditAsync(user.Id, "Password Changed", "Password changed successfully");
                await _signInManager.RefreshSignInAsync(user);

                SuccessMessage = "Password changed successfully! You can change it again after 5 minutes.";
                return Page();
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

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