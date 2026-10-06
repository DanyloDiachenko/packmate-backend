

using MailKit.Net.Smtp;
using MailKit.Security;
using MailService.DTOs;
using MailService.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MailService.Services;
public class EmailSettings
{
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "Packmate Travel";
    public string AppPassword { get; set; } = string.Empty;
}

public class GmailEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<GmailEmailService> _logger;

    public GmailEmailService(IOptions<EmailSettings> options, ILogger<GmailEmailService> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task SendCodeEmailAsync(SendCodeEmailRequest request, CancellationToken ct = default)
    {
        var (subject, htmlBody) = GenerateEmailContent(request);
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
        message.To.Add(new MailboxAddress(request.RecipientName ?? request.To, request.To));
        message.Subject = subject;
        var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
        message.Body = bodyBuilder.ToMessageBody();
        var password = _settings.AppPassword?.Replace(" ", "").Trim() ?? string.Empty;
        var senderEmail = _settings.SenderEmail?.Trim() ?? string.Empty;

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(senderEmail, password, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
            _logger.LogInformation("Sent {Type} email code to {Email}", request.Type, request.To);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deliver email to {Email}", request.To);
            throw;
        }
    }

    private static (string Subject, string HtmlBody) GenerateEmailContent(SendCodeEmailRequest req)
    {
        return req.Type switch
        {
            EmailTemplateType.RegisterVerification => (
                "Packmate — Verify Your Email Address",
                BuildTemplate(
                    title: "Welcome to Packmate!",
                    intro: "Thank you for joining. Use this code to verify your email and complete your registration:",
                    code: req.Code,
                    footer: "Valid for 10 minutes. If you did not sign up for Packmate, ignore this email."
                )
            ),
            EmailTemplateType.Login2FA => (
                "Packmate — Your Security Login Code",
                BuildTemplate(
                    title: "Two-Factor Verification",
                    intro: "A sign-in attempt was detected. Enter this 6-digit code to complete your login:",
                    code: req.Code,
                    footer: "Valid for 5 minutes. Never share this code with anyone."
                )
            ),
            EmailTemplateType.PasswordReset => (
                "Packmate — Password Reset Request",
                BuildTemplate(
                    title: "Reset Your Password",
                    intro: "We received a request to reset your Packmate account password. Use this code to continue:",
                    code: req.Code,
                    footer: "Valid for 15 minutes. If you didn't request a password reset, no action is needed."
                )
            ),
            _ => ("Packmate Verification Code", BuildTemplate("Verification Code", "Your code is:", req.Code, ""))
        };
    }

    private static string BuildTemplate(string title, string intro, string code, string footer)
    {
        return $$"""
        <!DOCTYPE html>
        <html>
        <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f7fafc; margin: 0; padding: 40px 16px;">
          <div style="max-width: 480px; margin: 0 auto; background: #ffffff; border-radius: 12px; padding: 36px 28px; box-shadow: 0 4px 14px rgba(0,0,0,0.06); border: 1px solid #e2e8f0;">
            <div style="text-align: center; margin-bottom: 24px;">
              <span style="font-size: 24px; font-weight: 800; color: #2b6cb0; letter-spacing: -0.5px;">Packmate</span>
              <h2 style="color: #1a202c; margin: 16px 0 0; font-size: 20px;">{{title}}</h2>
            </div>
            <p style="color: #4a5568; font-size: 15px; line-height: 1.5; margin: 0 0 24px;">
              {{intro}}
            </p>
            <div style="background: #edf2f7; border: 1px dashed #cbd5e0; border-radius: 8px; padding: 18px; text-align: center; letter-spacing: 8px; font-size: 32px; font-weight: 700; color: #2b6cb0; margin-bottom: 24px;">
              {{code}}
            </div>
            <p style="color: #718096; font-size: 13px; margin: 0; line-height: 1.4; text-align: center;">
              {{footer}}
            </p>
          </div>
        </body>
        </html>
        """;
    }

}