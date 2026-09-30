namespace UserService.DTOs;

/// <summary>
/// Request payload for creating a new user account.
/// </summary>
/// <param name="Email">User email address (e.g. user@example.com).</param>
/// <param name="Password">User password (minimum 6 characters).</param>
public record SignUpRequest(string Email, string Password);

/// <summary>
/// Request payload for authenticating an existing user.
/// </summary>
/// <param name="Email">User email address.</param>
/// <param name="Password">User password.</param>
public record SignInRequest(string Email, string Password);

/// <summary>
/// Authentication response containing JWT access token and user identity details.
/// </summary>
/// <param name="Token">JWT Bearer access token.</param>
/// <param name="UserId">Unique user identifier.</param>
/// <param name="Email">User email address.</param>
public record AuthResponse(string Token, Guid UserId, string Email);

/// <summary>
/// User profile information for the authenticated user.
/// </summary>
/// <param name="UserId">Unique user identifier.</param>
/// <param name="Email">User email address.</param>
/// <param name="CreatedAt">Account creation timestamp in UTC.</param>
public record UserProfileResponse(Guid UserId, string Email, DateTime CreatedAt);

/// <summary>
/// Standard error response containing a descriptive error message.
/// </summary>
/// <param name="Message">Descriptive error message.</param>
public record ErrorResponse(string Message);