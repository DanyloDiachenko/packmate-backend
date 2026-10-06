

using Microsoft.AspNetCore.Mvc;
using MailService.DTOs;
using MailService.Interfaces;

namespace MailService.Endpoints;

public static class MailEndpoints
{
    public static RouteGroupBuilder MapMailEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Mail");

        group.MapPost("/send-code", async (
            [FromBody] SendCodeEmailRequest request,
            IEmailService emailService,
            CancellationToken ct
        ) =>
        {
            if (string.IsNullOrWhiteSpace(request.To) || string.IsNullOrWhiteSpace(request.Code))
            {
                return Results.BadRequest(new SendEmailResponse(false, "Recipient email and code are required."));
            }
            try
            {
                await emailService.SendCodeEmailAsync(request, ct);
                return Results.Ok(new SendEmailResponse(true, "Email sent successfully."));
            }
            catch (Exception ex)
            {
                return Results.Problem(statusCode: StatusCodes.Status502BadGateway, detail: ex.Message);
            }
        })
        .WithName("SendCodeEmail")
        .WithSummary("Send email with verification/login/reset code")
        .Produces<SendEmailResponse>(StatusCodes.Status200OK)
        .Produces<SendEmailResponse>(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status502BadGateway);

        return group;
    }
}