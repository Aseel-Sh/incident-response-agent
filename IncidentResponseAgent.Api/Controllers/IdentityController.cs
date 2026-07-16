using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IncidentResponseAgent.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/identity")]
public sealed class IdentityController : ControllerBase
{
	[HttpGet("me")]
	public ActionResult<object> GetMe() => Ok(new { name = User.Identity?.Name, roles = User.Claims.Where(claim => claim.Type == System.Security.Claims.ClaimTypes.Role).Select(claim => claim.Value).ToArray() });
}
