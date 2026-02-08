using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using AceJobAgency_AS_Assignment.Models;
using AceJobAgency_AS_Assignment.ViewModels;
using AceJobAgency_AS_Assignment.Data;
using AceJobAgency_AS_Assignment.Services;
using Ganss.Xss;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace AceJobAgency_AS_Assignment.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly IDataProtector _protector;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly ICodeGeneratorService _codeGenerator;
        private readonly AuthDbContext _context;
        private readonly ILogger<RegisterModel> _logger;

        [BindProperty]
        public ViewModels.Register Input { get; set; } = new();

        [BindProperty]
        public string? RecaptchaToken { get; set; }

        public string? RecaptchaSiteKey { get; set; }

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            IDataProtectionProvider dataProtectionProvider,
            IConfiguration configuration,
            IEmailService emailService,
            ICodeGeneratorService codeGenerator,
            AuthDbContext context,
            ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _environment = environment;
            _configuration = configuration;
            _emailService = emailService;
            _codeGenerator = codeGenerator;
            _context = context;
            _logger = logger;

            _protector = dataProtectionProvider.CreateProtector("AceJobAgency.NRICProtection");
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
                ModelState.AddModelError(string.Empty, "reCAPTCHA verification failed. Please try again.");
                return Page();
            }

            int passwordScore = CheckPasswordComplexity(Input.Password);
            if (passwordScore < 5)
            {
                ModelState.AddModelError("Input.Password", "Password does not meet complexity requirements.");
                return Page();
            }

            var sanitizer = new HtmlSanitizer();
            var sanitizedFirstName = sanitizer.Sanitize(Input.FirstName);
            var sanitizedLastName = sanitizer.Sanitize(Input.LastName);
            var sanitizedWhoAmI = Input.WhoAmI != null ? sanitizer.Sanitize(Input.WhoAmI) : null;

            string? resumePath = null;
            if (Input.Resume != null)
            {
                var allowedExtensions = new[] { ".pdf", ".docx" };
                var extension = Path.GetExtension(Input.Resume.FileName).ToLower();

                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("Input.Resume", "Only .pdf and .docx files are allowed.");
                    return Page();
                }

                if (Input.Resume.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("Input.Resume", "File size cannot exceed 5MB.");
                    return Page();
                }

                resumePath = await SaveResumeFile(Input.Resume);
            }

            var user = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                FirstName = sanitizedFirstName,
                LastName = sanitizedLastName,
                Gender = Input.Gender,
                NRIC = _protector.Protect(Input.NRIC),
                DateOfBirth = Input.DateOfBirth,
                ResumePath = resumePath,
                WhoAmI = sanitizedWhoAmI,
                PasswordLastChanged = DateTime.UtcNow,
                PasswordExpiryDate = DateTime.UtcNow.AddDays(90),
                IsEmailVerified = false,
                EmailConfirmed = false
            };

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                user.PasswordHistory = user.PasswordHash;
                await _userManager.UpdateAsync(user);

                var code = _codeGenerator.GenerateCode();

                var verificationCode = new EmailVerificationCode
                {
                    Email = user.Email!,
                    Code = code,
                    Type = CodeType.Registration,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    IsUsed = false,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                };

                _context.EmailVerificationCodes.Add(verificationCode);
                await _context.SaveChangesAsync();

                try
                {
                    await _emailService.SendVerificationCodeAsync(user.Email!, code, "registration");

                    TempData["Email"] = user.Email;
                    TempData["CodeType"] = "Registration";
                    return RedirectToPage("/VerifyCode");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to send verification email: {ex.Message}");
                    TempData["ErrorMessage"] = $"Registration successful but failed to send email. Your code is: {code}";
                    TempData["Email"] = user.Email;
                    TempData["CodeType"] = "Registration";
                    return RedirectToPage("/VerifyCode");
                }
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
            if (Regex.IsMatch(password, @"[@$!%*?&#]")) score++;
            return score;
        }

        private async Task<string> SaveResumeFile(IFormFile file)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "resumes");
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return "/uploads/resumes/" + uniqueFileName;
        }

        private async Task<bool> VerifyRecaptcha(string? token)
        {
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("reCAPTCHA token is null or empty");
                return false;
            }

            var secretKey = _configuration["RecaptchaSettings:SecretKey"];

            if (string.IsNullOrEmpty(secretKey))
            {
                _logger.LogError("reCAPTCHA secret key is not configured");
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

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError($"reCAPTCHA API returned status code: {response.StatusCode}");
                        return false;
                    }

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"reCAPTCHA Response: {jsonResponse}");

                    var result = JsonSerializer.Deserialize<RecaptchaResponse>(jsonResponse);

                    if (result == null)
                    {
                        _logger.LogError("Failed to deserialize reCAPTCHA response");
                        return false;
                    }

                    if (!result.success)
                    {
                        _logger.LogWarning($"reCAPTCHA verification failed. Success: {result.success}");
                        return false;
                    }

                    if (result.score < 0.5)
                    {
                        _logger.LogWarning($"reCAPTCHA score too low: {result.score}");
                        return false;
                    }

                    _logger.LogInformation($"reCAPTCHA verification successful. Score: {result.score}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"reCAPTCHA verification exception: {ex.Message}");
                return false;
            }
        }
    }
}