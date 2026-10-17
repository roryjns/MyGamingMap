using Microsoft.AspNetCore.Mvc;
using MyGamingMap.API.Services;
using MyGamingMap.API.Exceptions;

namespace MyGamingMap.API.Controllers;

[ApiController]
[Route("api")]
public class PlayerController(PlayerService playerService, RateLimiter rateLimiter) : ControllerBase
{
    private readonly PlayerService playerService = playerService;
    private readonly RateLimiter rateLimiter = rateLimiter;

    [HttpGet("{username}/analytics")]
    public async Task<IActionResult> GetAnalytics(string username)
    {
        username = username.Trim().ToLowerInvariant();

        var clientKey =
            HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        if (!await rateLimiter.TryAcquireUsernameLockAsync(username))
        {
            return Conflict(new
            {
                message = "Analytics are currently already being generated for this user."
            });
        }

        try
        {
            if (!rateLimiter.TryAcquireClient(clientKey))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    message = "Too many requests sent recently. Please try again later."
                });
            }

            if (!rateLimiter.TryAcquireUsername(username))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    message = "This user was recently processed. Please try again later."
                });
            }

            return await Execute(() => playerService.GetAnalytics(username));
        }
        finally
        {
            rateLimiter.ReleaseUsernameLock(username);
        }
    }

    [HttpGet("{username}/map")]
    public async Task<IActionResult> GetMap(string username)
    {
        username = username.Trim().ToLowerInvariant();

        var clientKey =
            HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        if (!await rateLimiter.TryAcquireUsernameLockAsync(username))
        {
            return Conflict(new
            {
                message = "A map is currently already being generated for this user."
            });
        }

        try
        {
            if (!rateLimiter.TryAcquireClient(clientKey))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    message = "Too many requests sent recently. Please try again later."
                });
            }

            if (!rateLimiter.TryAcquireUsername(username))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    message = "This user was recently processed. Please try again later."
                });
            }

            return await Execute(() => playerService.GetMap(username));
        }
        finally
        {
            rateLimiter.ReleaseUsernameLock(username);
        }
    }

    [HttpGet("benchmark-test")]
    public async Task<IActionResult> BenchmarkTest()
    {
        if (!HttpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>()
            .IsDevelopment())
        {
            return NotFound();
        }

        await playerService.BenchmarkTest();
        return Ok();
    }

    [HttpPost("update-npsso")]
    public async Task<IActionResult> RefreshNPSSO(string npsso)
    {
        try
        {
            await playerService.RefreshNPSSO(npsso);
            return Ok();
        }
        catch (InvalidNpssoException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// Wraps a service call and translates known exceptions into
    /// consistent HTTP responses. Any other exception is rethrown
    /// so the framework surfaces it as a 500.
    private async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try
        {
            var result = await action();
            return Ok(result);
        }
        catch (PlayerNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (NoGamesFoundException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (PSNAuthenticationFailed ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = ex.Message });
        }
        catch (RateLimitExceededException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { message = ex.Message });
        }
    }
}