using System.Net.Http.Json;

namespace UserService.Extensions;

public static class MailClientExtensions
{
    public static async Task SendCodeViaMailServiceAsync(
        this IHttpClientFactory factory,
        string email,
        string code,
        int type,
        CancellationToken ct = default)
    {
        var client = factory.CreateClient("MailService");
        var payload = new
        {
            to = email,
            code = code,
            type = type
        };

        var response = await client.PostAsJsonAsync("/mail/send-code", payload, ct);
        response.EnsureSuccessStatusCode();
    }
}