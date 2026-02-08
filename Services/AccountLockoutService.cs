using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AceJobAgency_AS_Assignment.Models;
using AceJobAgency_AS_Assignment.Data;

namespace AceJobAgency_AS_Assignment.Services
{
    public class AccountLockoutService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AccountLockoutService> _logger;

        public AccountLockoutService(
            IServiceProvider serviceProvider,
            ILogger<AccountLockoutService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await CheckAndUnlockAccounts();
                await CleanupStaleSessions();
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task CheckAndUnlockAccounts()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

                    var lockedUsers = await context.Users
                        .Where(u => u.LockoutEnd != null && u.LockoutEnd <= DateTimeOffset.UtcNow)
                        .ToListAsync();

                    foreach (var user in lockedUsers)
                    {
                        await userManager.SetLockoutEndDateAsync(user, null);
                        await userManager.ResetAccessFailedCountAsync(user);

                        _logger.LogInformation($"Account unlocked for user: {user.Email}");

                        var auditLog = new AuditLog
                        {
                            UserId = user.Id,
                            Action = "Account Auto-Unlocked",
                            Timestamp = DateTime.UtcNow
                        };
                        context.AuditLogs.Add(auditLog);
                    }

                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CheckAndUnlockAccounts");
            }
        }

        private async Task CleanupStaleSessions()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

                    var staleSessionCutoff = DateTime.UtcNow.AddMinutes(-2);

                    var staleSessions = await context.LoginHistories
                        .Where(l => l.IsActive && l.LoginTime < staleSessionCutoff)
                        .ToListAsync();

                    if (staleSessions.Any())
                    {
                        foreach (var session in staleSessions)
                        {
                            session.IsActive = false;
                            session.LogoutTime = DateTime.UtcNow;
                        }

                        await context.SaveChangesAsync();
                        _logger.LogInformation($"Cleaned up {staleSessions.Count} stale session(s)");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CleanupStaleSessions");
            }
        }
    }
}