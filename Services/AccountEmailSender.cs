using System.Net;
using System.Net.Mail;

namespace BirdieBuddy.Services;

public interface IAccountEmailSender
{
    Task SendPasswordResetAsync(string email, string resetUrl);
    Task SendEmailVerificationAsync(string email, string verificationUrl);
}

public sealed class SmtpAccountEmailSender : IAccountEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpAccountEmailSender> _logger;

    public SmtpAccountEmailSender(IConfiguration configuration, ILogger<SmtpAccountEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task SendPasswordResetAsync(string email, string resetUrl) =>
        SendAsync(email, "Reset your Birdie Buddy password",
            $"Open this link to reset your password. It expires in 30 minutes:\n\n{resetUrl}\n\nIf you did not request this, ignore this email.");

    public Task SendEmailVerificationAsync(string email, string verificationUrl) =>
        SendAsync(email, "Verify your Birdie Buddy email",
            $"Open this link to verify your email address. It expires in 24 hours:\n\n{verificationUrl}");

    private async Task SendAsync(string recipient, string subject, string body)
    {
        var host = _configuration["Email:Smtp:Host"];
        var from = _configuration["Email:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            _logger.LogWarning("Account email was not sent because SMTP is not configured. Recipient domain: {Domain}",
                recipient.Split('@').LastOrDefault() ?? "unknown");
            return;
        }

        using var client = new SmtpClient(host, _configuration.GetValue("Email:Smtp:Port", 587))
        {
            EnableSsl = _configuration.GetValue("Email:Smtp:EnableSsl", true)
        };
        var username = _configuration["Email:Smtp:Username"];
        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, _configuration["Email:Smtp:Password"]);
        using var message = new MailMessage(from, recipient, subject, body);
        await client.SendMailAsync(message);
    }
}
