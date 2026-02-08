using Microsoft.AspNetCore.Mvc;

namespace AceJobAgency_AS_Assignment.Api
{
    [Route("api")]
    [ApiController]
    public class SessionController : ControllerBase
    {
        [HttpPost("keepalive")]
        public IActionResult KeepAlive()
        {
            // Just accessing the endpoint keeps the session alive
            return Ok(new { message = "Session refreshed" });
        }

        [HttpGet("checksession")]
        public IActionResult CheckSession()
        {
            var sessionId = HttpContext.Session.GetString("SessionId");
            var isAuthenticated = User.Identity?.IsAuthenticated ?? false;

            return Ok(new
            {
                isValid = isAuthenticated && !string.IsNullOrEmpty(sessionId)
            });
        }
    }
}