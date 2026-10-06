using MailService.DTOs;

namespace MailService.Interfaces;

public interface IEmailService
{
    Task SendCodeEmailAsync(SendCodeEmailRequest request, CancellationToken ct = default);
}