using Microsoft.AspNetCore.Mvc;

namespace HealthBot.Api.Controllers;

[ApiController]
[Route("ping")]
public class PingController : ControllerBase
{
    [HttpGet]
    public IActionResult Ping() => Ok("pong");
}
