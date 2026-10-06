namespace UserService.DTOs;

/// <summary>
/// Request payload for creating a new user account.
/// </summary>
/// <param name="FirstName">User first name.</param>
/// <param name="LastName">User last name.</param>
/// <param name="Email">User email address (e.g. user@example.com).</param>
/// <param name="Password">User password (minimum 6 characters).</param>
public record SignUpRequest(string FirstName, string LastName, string Email, string Password);

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
/// <param name="FirstName">User first name.</param>
/// <param name="LastName">User last name.</param>
/// <param name="Email">User email address.</param>
/// <param name="CreatedAt">Account creation timestamp in UTC.</param>
public record UserProfileResponse(Guid UserId, string FirstName, string LastName, string Email, DateTime CreatedAt);

/// <summary>
/// Standard error response containing a descriptive error message.
/// </summary>
/// <param name="Message">Descriptive error message.</param>
public record ErrorResponse(string Message);

/// <summary>
/// Response indicating that two-factor verification code was sent to the user's email.
/// </summary>
/// <param name="RequiresTwoFactor">Whether 2FA code is required.</param>
/// <param name="Email">User email address.</param>
/// <param name="Message">User-friendly status message.</param>
public record LoginTwoFactorResponse(bool RequiresTwoFactor, string Email, string Message);

/// <summary>
/// Request payload to confirm email address with 6-digit code.
/// </summary>
/// <param name="Email">User email address.</param>
/// <param name="Code">6-digit verification code.</param>
public record ConfirmEmailRequest(string Email, string Code);

/// <summary>
/// Request payload to verify 2FA login code.
/// </summary>
/// <param name="Email">User email address.</param>
/// <param name="Code">6-digit 2FA code.</param>
public record VerifyLoginRequest(string Email, string Code);

/// <summary>
/// Request payload to initiate password reset.
/// </summary>
/// <param name="Email">User email address.</param>
public record ForgotPasswordRequest(string Email);

/// <summary>
/// Request payload to reset password with verification code.
/// </summary>
/// <param name="Email">User email address.</param>
/// <param name="Code">6-digit reset code.</param>
/// <param name="NewPassword">New password (minimum 6 characters).</param>
public record ResetPasswordRequest(string Email, string Code, string NewPassword);

/// <summary>
/// Request payload to resend a verification code.
/// </summary>
/// <param name="Email">User email address.</param>
/// <param name="Purpose">Code purpose ("register", "login", or "reset").</param>
public record ResendCodeRequest(string Email, string Purpose);