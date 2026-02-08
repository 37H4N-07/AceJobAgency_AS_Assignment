namespace AceJobAgency_AS_Assignment.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
        Task SendVerificationCodeAsync(string toEmail, string code, string purpose);
    }
}