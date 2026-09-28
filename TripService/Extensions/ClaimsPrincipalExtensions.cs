using System.Security.Claims;

namespace TripService.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var claimValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(claimValue) || !Guid.TryParse(claimValue, out var userId))
        {
            throw new UnauthorizedAccessException("User ID is missing or invalid in token claims");
        }

        return userId;
    }
}