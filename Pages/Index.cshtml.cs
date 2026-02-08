using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using AceJobAgency_AS_Assignment.Models;
using AceJobAgency_AS_Assignment.Data;

namespace AceJobAgency_AS_Assignment.Pages
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IDataProtector _protector;
        private readonly AuthDbContext _context;

        public ApplicationUser? CurrentUser { get; set; }
        public string DecryptedNRIC { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            IDataProtectionProvider dataProtectionProvider,
            AuthDbContext context)
        {
            _userManager = userManager;
            _context = context;
            _protector = dataProtectionProvider.CreateProtector("AceJobAgency.NRICProtection");
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.Identity!.IsAuthenticated)
            {
                CurrentUser = await _userManager.GetUserAsync(User);

                if (CurrentUser != null)
                {
                    try
                    {
                        DecryptedNRIC = _protector.Unprotect(CurrentUser.NRIC);
                    }
                    catch
                    {
                        DecryptedNRIC = "Error decrypting NRIC";
                    }

                    SessionId = HttpContext.Session.GetString("SessionId") ?? "No active session";
                }
            }

            return Page();
        }

        public int CalculateAge()
        {
            if (CurrentUser == null)
                return 0;

            var today = DateTime.Today;
            var age = today.Year - CurrentUser.DateOfBirth.Year;
            if (CurrentUser.DateOfBirth.Date > today.AddYears(-age))
                age--;

            return age;
        }
    }
}