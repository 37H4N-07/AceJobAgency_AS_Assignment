using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AceJobAgency_AS_Assignment.Models;
using AceJobAgency_AS_Assignment.ViewModels;
using AceJobAgency_AS_Assignment.Data;
using AceJobAgency_AS_Assignment.Services;
using System.Text.Json;

namespace AceJobAgency_AS_Assignment.Pages
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuthDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly ICodeGeneratorService _codeGenerator;

        [BindProperty]
        public ViewModels.Login Input { get; set; } = new();

        [BindProperty]
        public string? RecaptchaToken { get; set; }

        public string? RecaptchaSiteKey { get; set; }
        public string? ErrorMessage { get; set; }

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            AuthDbContext context,
            IConfiguration configuration,
            IEmailService emailService,
            ICodeGeneratorService codeGenerator)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
            _codeGenerator = codeGenerator;
            RecaptchaSiteKey = _configuration["RecaptchaSettings:SiteKey"];
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

            if (!await VerifyRecaptcha(RecaptchaToken))
            {
                ErrorMessage = "reCAPTCHA verification failed. Please try again.";
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);

            if (user != null)
            {
                if (await _userManager.IsLockedOutAsync(user))
                {
                    var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                    if (lockoutEnd.HasValue)
                    {
                        var remainingTime = lockoutEnd.Value - DateTimeOffset.UtcNow;
                        ErrorMessage = $"Account is locked. Please try again in {remainingTime.Minutes} minutes.";
                        await LogAuditAsync(user.Id, "Failed Login - Account Locked", "Login attempt on locked account");
                        return Page();
                    }
                }

                var activeLogins = await _context.LoginHistories
                    .Where(l => l.UserId == user.Id && l.IsActive)
                    .ToListAsync();

                if (activeLogins.Any())
                {
                    ErrorMessage = "You are already logged in from another device/browser. Please logout from other sessions first.";
                    await LogAuditAsync(user.Id, "Failed Login - Multiple Sessions", "Attempted login while active session exists");
                    return Page();
                }

                var passwordCheck = await _userManager.CheckPasswordAsync(user, Input.Password);

                if (!passwordCheck)
                {
                    await _userManager.AccessFailedAsync(user);
                    var failedCount = await _userManager.GetAccessFailedCountAsync(user);
                    var remainingAttempts = 3 - failedCount;

                    if (remainingAttempts <= 0)
                    {
                        ErrorMessage = "Account locked due to multiple failed login attempts. Please try again after 5 minutes.";
                        await LogAuditAsync(user.Id, "Account Locked Out", "Account locked after 3 failed attempts");
                    }
                    else
                    {
                        ErrorMessage = $"Invalid login attempt. {remainingAttempts} attempt(s) remaining before account lockout.";
                        await LogAuditAsync(user.Id, "Failed Login", $"Invalid password. Remaining attempts: {remainingAttempts}");
                    }

                    return Page();
                }

                if (!user.IsEmailVerified)
                {
                    ErrorMessage = "Please verify your email address before logging in. Check your inbox for the verification code.";
                    await LogAuditAsync(user.Id, "Failed Login - Email Not Verified", "Login attempt with unverified email");
                    return Page();
                }

                var code = _codeGenerator.GenerateCode();

                var verificationCode = new EmailVerificationCode
                {
                    Email = user.Email!,
                    Code = code,
                    Type = CodeType.Login2FA,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    IsUsed = false,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                };

                _context.EmailVerificationCodes.Add(verificationCode);
                await _context.SaveChangesAsync();

                try
                {
                    await _emailService.SendVerificationCodeAsync(user.Email!, code, "login");
                }
                catch
                {
                    TempData["ErrorMessage"] = $"Failed to send email. Your login code is: {code}";
                }

                await LogAuditAsync(user.Id, "2FA Code Sent", "Login 2FA code generated and sent");
                await _userManager.ResetAccessFailedCountAsync(user);

                TempData["Email"] = user.Email;
                TempData["CodeType"] = "Login2FA";
                return RedirectToPage("/VerifyCode");
            }
            else
            {
                ErrorMessage = "Invalid login attempt.";
            }

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

        private async Task<bool> VerifyRecaptcha(string? token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            var secretKey = _configuration["RecaptchaSettings:SecretKey"];

            if (string.IsNullOrEmpty(secretKey))
            {
                return false;
            }

            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(10);

                    var response = await httpClient.PostAsync(
                        $"https://www.google.com/recaptcha/api/siteverify?secret={secretKey}&response={token}",
                        null);

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<RecaptchaResponse>(jsonResponse);

                    return result?.success == true && result?.score >= 0.5;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"reCAPTCHA Error: {ex.Message}");
                return false;
            }
        }
    }
}