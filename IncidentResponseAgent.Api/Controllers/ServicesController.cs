using IncidentResponseAgent.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IncidentResponseAgent.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/services")]
public sealed class ServicesController(IServiceCatalog catalog) : ControllerBase
{
	[HttpGet]
	public ActionResult<IReadOnlyList<ServiceCatalogEntry>> Get() => Ok(catalog.GetServices());
}
