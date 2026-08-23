using Microsoft.AspNetCore.Mvc;
using MyGamingMap.API.Services;

namespace MyGamingMap.API.Controllers;

[ApiController]
[Route("api")]
public class PlayerController(PlayerService playerService) : ControllerBase
{
    private readonly PlayerService playerService = playerService;
    
    [HttpGet("{username}/map")]
    public async Task<IActionResult> GetMap(string username)
    {
        var map = await playerService.GetMap(username);
        return Ok(map);
    }

    [HttpPost("benchmark-test")]
    public async Task<IActionResult> BenchmarkTest()
    {
        await playerService.BenchmarkTest();
        return Ok();
    }
}