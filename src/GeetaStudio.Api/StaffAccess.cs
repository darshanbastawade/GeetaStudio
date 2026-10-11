using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace GeetaStudio.Api;

public sealed class StaffAccess(IConfiguration configuration) : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not { IsAuthenticated: true, Name: not null }) return Task.FromResult(principal);
        // Only explicit staff mappings grant product roles; Windows group names cannot grant them implicitly.
        var roles = new ClaimsIdentity(principal.Claims.Where(c => c.Type != ClaimTypes.Role && c.Type != ClaimTypes.GroupSid),
            principal.Identity.AuthenticationType, ClaimTypes.Name, ClaimTypes.Role);
        foreach (var role in new[] { "Administrator", "Operator", "Reader" })
        {
            if (configuration.GetSection("Staff:" + role).Get<string[]>()?.Contains(principal.Identity.Name, StringComparer.OrdinalIgnoreCase) == true)
                roles.AddClaim(new Claim(ClaimTypes.Role, role));
        }
        return Task.FromResult(new ClaimsPrincipal(roles));
    }
}
