using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using UserService.DTOs;
using UserService.Data;
using UserService.Services;
using UserService.Entities;
using UserService.Extensions;

namespace UserService.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Authentication");

        group.MapPost("/sign-up", async (
            SignUpRequest request,
            IValidator<SignUpRequest> validator,
            UserDbContext db,
            IVerificationCodeService codeService,
            IHttpClientFactory httpClientFactory,
            CancellationToken ct
        ) =>
        {
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var emailExists = await db.Users.AnyAsync(u => u.Email == request.Email, ct);
            if (emailExists)
            {
                return Results.Conflict(new ErrorResponse("Email already exists"));
            }

            var newUser = new User
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = request.Email.Trim().ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                IsEmailConfirmed = false,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(newUser);
            await db.SaveChangesAsync(ct);

            var code = await codeService.GenerateAndSaveCodeAsync(newUser.Email, "register", ct: ct);
            await httpClientFactory.SendCodeViaMailServiceAsync(newUser.Email, code, type: 0, ct: ct);

            return Results.Ok(new
            {
                message = "Registration successful. Please check your email for the verification code.",
                email = newUser.Email
            });
        })
        .WithName("SignUp")
        .WithSummary("Register a new user account and send email verification code")
        .WithDescription("Creates a new user account with hashed password and dispatches a verification code to user email.")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        group.MapPost("/confirm-email", async (
            ConfirmEmailRequest request,
            UserDbContext db,
            IVerificationCodeService codeService,
            ITokenService tokenService,
            CancellationToken ct
        ) =>
        {
            var isValid = await codeService.ValidateCodeAsync(request.Email, request.Code, "register", ct);
            if (!isValid)
            {
                return Results.BadRequest(new ErrorResponse("Invalid or expired verification code."));
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLowerInvariant(), ct);
            if (user == null)
            {
                return Results.NotFound(new ErrorResponse("User not found."));
            }

            user.IsEmailConfirmed = true;
            await db.SaveChangesAsync(ct);

            var token = tokenService.GenerateToken(user);
            return Results.Ok(new AuthResponse(token, user.Id, user.Email));
        })
        .WithName("ConfirmEmail")
        .WithSummary("Verify registration email code and issue JWT token")
        .WithDescription("Validates the email registration code, activates the account, and returns an access token.")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/sign-in", async (
            SignInRequest request,
            IValidator<SignInRequest> validator,
            UserDbContext db,
            IVerificationCodeService codeService,
            IHttpClientFactory httpClientFactory,
            CancellationToken ct
        ) =>
        {
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLowerInvariant(), ct);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Results.Unauthorized();
            }

            if (!user.IsEmailConfirmed)
            {
                var registerCode = await codeService.GenerateAndSaveCodeAsync(user.Email, "register", ct: ct);
                await httpClientFactory.SendCodeViaMailServiceAsync(user.Email, registerCode, type: 0, ct: ct);

                return Results.BadRequest(new ErrorResponse("Email not confirmed. A new verification code was sent to your email."));
            }

            var code = await codeService.GenerateAndSaveCodeAsync(user.Email, "login", ct: ct);
            await httpClientFactory.SendCodeViaMailServiceAsync(user.Email, code, type: 1, ct: ct);

            return Results.Ok(new LoginTwoFactorResponse(
                RequiresTwoFactor: true,
                Email: user.Email,
                Message: "A login verification code was sent to your email."
            ));
        })
        .WithName("SignIn")
        .WithSummary("Validate credentials and send 2FA login code")
        .WithDescription("Validates email and password, then sends a 6-digit login code to user email.")
        .Produces<LoginTwoFactorResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/verify-login", async (
            VerifyLoginRequest request,
            UserDbContext db,
            IVerificationCodeService codeService,
            ITokenService tokenService,
            CancellationToken ct
        ) =>
        {
            var isValid = await codeService.ValidateCodeAsync(request.Email, request.Code, "login", ct);
            if (!isValid)
            {
                return Results.BadRequest(new ErrorResponse("Invalid or expired login code."));
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLowerInvariant(), ct);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            var token = tokenService.GenerateToken(user);
            return Results.Ok(new AuthResponse(token, user.Id, user.Email));
        })
        .WithName("VerifyLogin")
        .WithSummary("Verify 2FA login code and receive JWT token")
        .WithDescription("Confirms the 2FA code sent to email during login and returns an authentication JWT token.")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/forgot-password", async (
            ForgotPasswordRequest request,
            UserDbContext db,
            IVerificationCodeService codeService,
            IHttpClientFactory httpClientFactory,
            CancellationToken ct
        ) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLowerInvariant(), ct);

            if (user != null)
            {
                var code = await codeService.GenerateAndSaveCodeAsync(user.Email, "reset", ct: ct);
                await httpClientFactory.SendCodeViaMailServiceAsync(user.Email, code, type: 2, ct: ct);
            }

            return Results.Ok(new
            {
                message = "If an account exists with this email, a password reset code has been sent."
            });
        })
        .WithName("ForgotPassword")
        .WithSummary("Request password reset code to email")
        .WithDescription("Sends a 6-digit password reset code to the provided email if the account exists.")
        .Produces(StatusCodes.Status200OK);

        group.MapPost("/reset-password", async (
            ResetPasswordRequest request,
            UserDbContext db,
            IVerificationCodeService codeService,
            CancellationToken ct
        ) =>
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            {
                return Results.BadRequest(new ErrorResponse("Password must be at least 6 characters."));
            }

            var isValid = await codeService.ValidateCodeAsync(request.Email, request.Code, "reset", ct);
            if (!isValid)
            {
                return Results.BadRequest(new ErrorResponse("Invalid or expired password reset code."));
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLowerInvariant(), ct);
            if (user == null)
            {
                return Results.NotFound(new ErrorResponse("User not found."));
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                message = "Password successfully reset. You can now log in with your new password."
            });
        })
        .WithName("ResetPassword")
        .WithSummary("Confirm reset code and update password")
        .WithDescription("Verifies the reset code and updates the user password.")
        .Produces(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/resend-code", async (
            ResendCodeRequest request,
            UserDbContext db,
            IVerificationCodeService codeService,
            IHttpClientFactory httpClientFactory,
            CancellationToken ct
        ) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLowerInvariant(), ct);
            if (user == null)
            {
                return Results.NotFound(new ErrorResponse("User not found."));
            }

            var type = request.Purpose.ToLowerInvariant() switch
            {
                "register" => 0,
                "login" => 1,
                "reset" => 2,
                _ => -1
            };

            if (type == -1)
            {
                return Results.BadRequest(new ErrorResponse("Invalid purpose. Use 'register', 'login', or 'reset'."));
            }

            var code = await codeService.GenerateAndSaveCodeAsync(user.Email, request.Purpose, ct: ct);
            await httpClientFactory.SendCodeViaMailServiceAsync(user.Email, code, type: type, ct: ct);

            return Results.Ok(new { message = "Verification code resent." });
        })
        .WithName("ResendCode")
        .WithSummary("Resend verification code for register, login, or reset")
        .WithDescription("Resends a fresh code for registration, 2FA login, or password reset.")
        .Produces(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/profile", async (
            UserDbContext db,
            ClaimsPrincipal userClaims,
            CancellationToken ct
        ) =>
        {
            var userId = userClaims.GetUserId();
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user == null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new UserProfileResponse(user.Id, user.FirstName, user.LastName, user.Email, user.CreatedAt));
        })
        .RequireAuthorization()
        .WithName("GetUserProfile")
        .WithSummary("Get authenticated user profile")
        .WithDescription("Retrieves profile information for the user identified by the Bearer JWT token.")
        .Produces<UserProfileResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return group;
    }
}