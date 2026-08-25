using AiEngineering.HandsOn.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AiEngineering.HandsOn.Api.Controllers;

[ApiController]
[Route("api/v1/system")]
public sealed class SystemController(IHostEnvironment environment) : ControllerBase
{
    [HttpGet("health")]
    public ActionResult<object> Health() => Ok(new
    {
        status = "ok",
        service = "ai-engineering-hands-on-api",
        utcTime = DateTimeOffset.UtcNow
    });

    [HttpGet("info")]
    public ActionResult<ApiInfoResponse> Info() => Ok(new ApiInfoResponse(
        "ai-engineering-hands-on-api",
        "v1",
        environment.EnvironmentName,
        DateTimeOffset.UtcNow));
}
