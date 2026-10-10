namespace UserService.DTOs;








public record SignUpRequest(string FirstName, string LastName, string Email, string Password);






public record SignInRequest(string Email, string Password);







public record AuthResponse(string Token, Guid UserId, string Email);









public record UserProfileResponse(Guid UserId, string FirstName, string LastName, string Email, DateTime CreatedAt);






public record UpdateProfileRequest(string FirstName, string LastName);





public record ErrorResponse(string Message);







public record LoginTwoFactorResponse(bool RequiresTwoFactor, string Email, string Message);






public record ConfirmEmailRequest(string Email, string Code);






public record VerifyLoginRequest(string Email, string Code);





public record ForgotPasswordRequest(string Email);







public record ResetPasswordRequest(string Email, string Code, string NewPassword);






public record ResendCodeRequest(string Email, string Purpose);