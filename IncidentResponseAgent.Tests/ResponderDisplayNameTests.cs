using System.Security.Claims;
using IncidentResponseAgent.Api.Services;

namespace IncidentResponseAgent.Tests;

public class ResponderDisplayNameTests
{
    [Theory]
    [InlineData("Aseel Sh", "aseel", "Aseel Sh")]
    [InlineData("someone@example.com", "aseel", "aseel")]
    [InlineData("someone@example.com", null, "Responder")]
    public void UsesHumanClaimsWithoutDerivingNamesFromEmail(string name, string? username, string expected)
    {
        var claims = new List<Claim> { new("name", name), new("sub", "auth0|stable-id") };
        if (username is not null) claims.Add(new("preferred_username", username));
        Assert.Equal(expected, ResponderDisplayName.FromClaims(new ClaimsPrincipal(new ClaimsIdentity(claims, "test", "name", "role"))));
    }
}
