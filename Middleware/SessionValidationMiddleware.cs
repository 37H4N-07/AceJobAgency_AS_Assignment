using Microsoft.AspNetCore.Identity;
using AceJobAgency_AS_Assignment.Models;

namespace AceJobAgency_AS_Assignment.Middleware
{
    public class SessionValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public SessionValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, SignInManager<ApplicationUser> signInManager)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var sessionId = context.Session.GetString("SessionId");

                if (string.IsNullOrEmpty(sessionId))
                {
                    await signInManager.SignOutAsync();
                    context.Session.Clear();
                    context.Response.Redirect("/Login");
                    return;
                }
            }

            await _next(context);
        }
    }
}