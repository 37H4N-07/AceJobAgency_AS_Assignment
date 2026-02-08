using MailKit.Net.Smtp;
using MimeKit;

namespace AceJobAgency_AS_Assignment.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private string RedactEmailForLogging(string email)
        {
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            {
                return email;
            }

            var parts = email.Split('@');
            var localPart = parts[0];
            var domainPart = parts[1];

            if (localPart.Length <= 1)
            {
                return $"*@{domainPart}";
            }

            var visibleFirstChar = localPart[0];
            var redactedLocal = visibleFirstChar + new string('*', localPart.Length - 1);

            return $"{redactedLocal}@{domainPart}";
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(
                    _configuration["EmailSettings:SenderName"],
                    _configuration["EmailSettings:SenderEmail"]
                ));
                message.To.Add(new MailboxAddress("", toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = body
                };
                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                    await client.ConnectAsync(
                        _configuration["EmailSettings:SmtpServer"],
                        int.Parse(_configuration["EmailSettings:SmtpPort"]!),
                        MailKit.Security.SecureSocketOptions.StartTls
                    );

                    await client.AuthenticateAsync(
                        _configuration["EmailSettings:Username"],
                        _configuration["EmailSettings:Password"]
                    );

                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }

                _logger.LogInformation("Email sent successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email.");
                throw;
            }
        }

        public async Task SendVerificationCodeAsync(string toEmail, string code, string purpose)
        {
            string subject = "";
            string body = "";

            switch (purpose.ToLower())
            {
                case "registration":
                    subject = "Verify Your Email - Ace Job Agency";
                    body = GenerateRegistrationEmailTemplate(code);
                    break;
                case "login":
                    subject = "Your Login Code - Ace Job Agency";
                    body = GenerateLoginEmailTemplate(code);
                    break;
                case "passwordreset":
                    subject = "Password Reset Code - Ace Job Agency";
                    body = GeneratePasswordResetEmailTemplate(code);
                    break;
            }

            await SendEmailAsync(toEmail, subject, body);
        }

        private string GenerateRegistrationEmailTemplate(string code)
        {
            return $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; background-color: #f4f4f4; }}
                        .container {{ max-width: 600px; margin: 30px auto; background: white; border-radius: 10px; overflow: hidden; box-shadow: 0 0 20px rgba(0,0,0,0.1); }}
                        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; }}
                        .header h1 {{ margin: 0; font-size: 28px; }}
                        .content {{ padding: 40px 30px; }}
                        .code-box {{ background: #f8f9fa; border: 2px dashed #667eea; border-radius: 8px; padding: 20px; text-align: center; margin: 30px 0; }}
                        .code {{ font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #667eea; font-family: 'Courier New', monospace; }}
                        .info {{ background: #e7f3ff; border-left: 4px solid #2196F3; padding: 15px; margin: 20px 0; border-radius: 4px; }}
                        .footer {{ background: #f8f9fa; padding: 20px; text-align: center; color: #666; font-size: 12px; }}
                        .button {{ display: inline-block; padding: 12px 30px; background: #667eea; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>🎉 Welcome to Ace Job Agency!</h1>
                        </div>
                        <div class='content'>
                            <h2>Verify Your Email Address</h2>
                            <p>Thank you for registering with Ace Job Agency. To complete your registration, please enter the verification code below:</p>
                            
                            <div class='code-box'>
                                <div style='color: #666; font-size: 14px; margin-bottom: 10px;'>Your Verification Code</div>
                                <div class='code'>{code}</div>
                            </div>

                            <div class='info'>
                                <strong>⏰ Important:</strong> This code will expire in 10 minutes.
                            </div>

                            <p>If you didn't create an account with Ace Job Agency, please ignore this email.</p>
                        </div>
                        <div class='footer'>
                            <p>© 2025 Ace Job Agency. All rights reserved.</p>
                            <p>This is an automated email, please do not reply.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private string GenerateLoginEmailTemplate(string code)
        {
            return $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; background-color: #f4f4f4; }}
                        .container {{ max-width: 600px; margin: 30px auto; background: white; border-radius: 10px; overflow: hidden; box-shadow: 0 0 20px rgba(0,0,0,0.1); }}
                        .header {{ background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%); color: white; padding: 30px; text-align: center; }}
                        .header h1 {{ margin: 0; font-size: 28px; }}
                        .content {{ padding: 40px 30px; }}
                        .code-box {{ background: #f8f9fa; border: 2px dashed #11998e; border-radius: 8px; padding: 20px; text-align: center; margin: 30px 0; }}
                        .code {{ font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #11998e; font-family: 'Courier New', monospace; }}
                        .warning {{ background: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; border-radius: 4px; }}
                        .footer {{ background: #f8f9fa; padding: 20px; text-align: center; color: #666; font-size: 12px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>🔐 Your Login Code</h1>
                        </div>
                        <div class='content'>
                            <h2>Two-Factor Authentication</h2>
                            <p>Someone is attempting to log in to your Ace Job Agency account. Please enter the code below to complete the login:</p>
                            
                            <div class='code-box'>
                                <div style='color: #666; font-size: 14px; margin-bottom: 10px;'>Your Login Code</div>
                                <div class='code'>{code}</div>
                            </div>

                            <div class='warning'>
                                <strong>⚠️ Security Notice:</strong><br>
                                If you didn't attempt to log in, please change your password immediately and contact support.
                            </div>

                            <p><strong>⏰ This code expires in 10 minutes.</strong></p>
                        </div>
                        <div class='footer'>
                            <p>© 2025 Ace Job Agency. All rights reserved.</p>
                            <p>This is an automated email, please do not reply.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private string GeneratePasswordResetEmailTemplate(string code)
        {
            return $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; background-color: #f4f4f4; }}
                        .container {{ max-width: 600px; margin: 30px auto; background: white; border-radius: 10px; overflow: hidden; box-shadow: 0 0 20px rgba(0,0,0,0.1); }}
                        .header {{ background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); color: white; padding: 30px; text-align: center; }}
                        .header h1 {{ margin: 0; font-size: 28px; }}
                        .content {{ padding: 40px 30px; }}
                        .code-box {{ background: #f8f9fa; border: 2px dashed #f5576c; border-radius: 8px; padding: 20px; text-align: center; margin: 30px 0; }}
                        .code {{ font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #f5576c; font-family: 'Courier New', monospace; }}
                        .warning {{ background: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; border-radius: 4px; }}
                        .footer {{ background: #f8f9fa; padding: 20px; text-align: center; color: #666; font-size: 12px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>🔑 Password Reset Request</h1>
                        </div>
                        <div class='content'>
                            <h2>Reset Your Password</h2>
                            <p>We received a request to reset your password. Please enter the code below to proceed:</p>
                            
                            <div class='code-box'>
                                <div style='color: #666; font-size: 14px; margin-bottom: 10px;'>Your Reset Code</div>
                                <div class='code'>{code}</div>
                            </div>

                            <div class='warning'>
                                <strong>⚠️ Security Notice:</strong><br>
                                If you didn't request a password reset, please ignore this email. Your password will remain unchanged.
                            </div>

                            <p><strong>⏰ This code expires in 10 minutes.</strong></p>
                        </div>
                        <div class='footer'>
                            <p>© 2025 Ace Job Agency. All rights reserved.</p>
                            <p>This is an automated email, please do not reply.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }
    }
}