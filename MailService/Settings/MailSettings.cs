namespace MailService.Settings;

public class MailSettings
{
    public string SmtpHost { get; set; } = "smtp@gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "Packmate Travel";
    public string AppPassword { get; set; } = string.Empty;
}
