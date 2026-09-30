using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using UserService.DTOs;
using UserService.Data;
using UserService.Services;
using UserService.Validators;
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
            ITokenService tokenService
        ) =>
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var emailExists = await db.Users.AnyAsync(u => u.Email == request.Email);
            if (emailExists)
            {
                return Results.Conflict(new ErrorResponse("Email already exists"));
            }

            var newUser = new User
            {
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(newUser);
            await db.SaveChangesAsync();

            var token = tokenService.GenerateToken(newUser);
            return Results.Ok(new AuthResponse(token, newUser.Id, newUser.Email));
        })
        .WithName("SignUp")
        .WithSummary("Register a new user account")
        .WithDescription("Creates a new user account with hashed password and returns an authentication JWT token.")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        group.MapPost("/sign-in", async
        (
            SignInRequest request,
            IValidator<SignInRequest> validator,
            UserDbContext db,
            ITokenService tokenService
        ) =>
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Results.Unauthorized();
            }

            var token = tokenService.GenerateToken(user);
            return Results.Ok(new AuthResponse(token, user.Id, user.Email));
        })
        .WithName("SignIn")
        .WithSummary("Authenticate user with credentials")
        .WithDescription("Validates user credentials and returns an authentication JWT token on success.")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/profile", async (
            UserDbContext db,
            ClaimsPrincipal userClaims
        ) =>
        {
            var userId = userClaims.GetUserId();
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new UserProfileResponse(user.Id, user.Email, user.CreatedAt));
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