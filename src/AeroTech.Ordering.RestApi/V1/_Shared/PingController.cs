using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace AeroTech.Ordering.RestApi.V1._Shared
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public sealed class PingController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get() => Ok(new { status = "ok" });
    }
}
