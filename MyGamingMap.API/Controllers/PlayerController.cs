using Microsoft.AspNetCore.Mvc;
using MyGamingMap.API.Services;

namespace MyGamingMap.API.Controllers;

[ApiController]
[Route("api")]
public class PlayerController(PlayerService playerService, RateLimiter rateLimiter) : ControllerBase
{
    private readonly PlayerService playerService = playerService;
    private readonly RateLimiter rateLimiter = rateLimiter;

    [HttpGet("{username}/map")]
    public async Task<IActionResult> GetMap(string username)
    {
        username = username.Trim().ToLowerInvariant();

        var clientKey =
            HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        // Prevent the same username being generated simultaneously
        if (!await rateLimiter.TryAcquireUsernameLockAsync(username))
        {
            return Conflict(new
            {
                message = "Analytics are currently already being generated for this user."
            });
        }

        try
        {
            // One request per client per minute
            if (!rateLimiter.TryAcquireClient(clientKey))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    message = "Please wait before generating another map."
                });
            }

            // One request per username per five minutes
            if (!rateLimiter.TryAcquireUsername(username))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    message = "This user was recently processed. Please try again later."
                });
            }

            var result = await playerService.GetMap(username);

            return Ok(result);
        }
        finally
        {
            rateLimiter.ReleaseUsernameLock(username);
        }
    }

    [HttpPost("benchmark-test")]
    public async Task<IActionResult> BenchmarkTest()
    {
        await playerService.BenchmarkTest();
        return Ok();
    }
}