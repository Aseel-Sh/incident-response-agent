using System.Security.Claims;

namespace IncidentResponseAgent.Api.Services;

public static class ResponderDisplayName
{
    public static string FromClaims(ClaimsPrincipal user)
    {
        var fullName = string.Join(" ", new[] { user.FindFirstValue("given_name") ?? user.FindFirstValue(ClaimTypes.GivenName), user.FindFirstValue("family_name") ?? user.FindFirstValue(ClaimTypes.Surname) }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var candidates = new[] { user.FindFirstValue("name"), user.FindFirstValue(ClaimTypes.Name), fullName, user.FindFirstValue("preferred_username"), user.FindFirstValue("username"), user.FindFirstValue("nickname"), user.Identity?.Name };
        return candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && !value.Contains('@') && !value.Contains('|'))?.Trim() ?? "Responder";
    }
}
