using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IncidentResponseAgent.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/identity")]
public sealed class IdentityController : ControllerBase
{
	[HttpGet("me")]
	public ActionResult<object> GetMe()
	{
		var roleClaimType = (User.Identity as System.Security.Claims.ClaimsIdentity)?.RoleClaimType
			?? System.Security.Claims.ClaimTypes.Role;
		return Ok(new
		{
			name = User.Identity?.Name,
			roles = User.Claims.Where(claim => claim.Type == roleClaimType).Select(claim => claim.Value).ToArray()
		});
	}
}
