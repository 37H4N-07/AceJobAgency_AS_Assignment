using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AceJobAgency_AS_Assignment.Models;
using AceJobAgency_AS_Assignment.Data;

namespace AceJobAgency_AS_Assignment.Pages
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuthDbContext _context;

        public LogoutModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            AuthDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.Identity!.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                var sessionId = HttpContext.Session.GetString("SessionId");

                if (user != null && !string.IsNullOrEmpty(sessionId))
                {
                    var loginHistory = await _context.LoginHistories
                        .FirstOrDefaultAsync(l => l.SessionId == sessionId && l.IsActive);

                    if (loginHistory != null)
                    {
                        loginHistory.IsActive = false;
                        loginHistory.LogoutTime = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }

                    var auditLog = new AuditLog
                    {
                        UserId = user.Id,
                        Action = "Logout",
                        Timestamp = DateTime.UtcNow,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        UserAgent = Request.Headers["User-Agent"].ToString()
                    };
                    _context.AuditLogs.Add(auditLog);
                    await _context.SaveChangesAsync();
                }

                HttpContext.Session.Clear();
                await _signInManager.SignOutAsync();
            }

            return RedirectToPage("/Login");
        }
    }
}