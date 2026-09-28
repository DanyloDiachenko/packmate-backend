namespace UserService.DTOs;

public record SignUpRequest(string Email, string Password);

public record SignInRequest(string Email, string Password);

public record AuthResponse(string Token, Guid UserId, string Email);

public record UserProfileResponse(Guid UserId, string Email, DateTime CreatedAt);