using System.Text.Json.Serialization;

namespace MailService.DTOs;

public enum EmailTemplateType
{
    RegisterVerification,
    Login2FA,
    PasswordReset
}

public record SendCodeEmailRequest(
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("type")] EmailTemplateType Type,
    [property: JsonPropertyName("recipientName")] string? RecipientName = null
);

public record SendEmailResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message
);
