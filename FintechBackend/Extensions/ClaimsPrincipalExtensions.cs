using System.Security.Claims;

namespace FintechBackend.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static string? GetUserId(this ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.NameIdentifier);// ?? throw new InvalidOperationException("User NameIdentifier claim is missing.");
        }
    }
}