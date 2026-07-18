using Microsoft.AspNetCore.Mvc;
using MyGamingMap.API.Services;

namespace MyGamingMap.API.Controllers;

[ApiController]
[Route("api")]
public class PlayerController(PSNServiceClient psnServiceClient) : ControllerBase
{
    private readonly PSNServiceClient _psnServiceClient = psnServiceClient;

    [HttpGet("{username}/games")]
    public async Task<IActionResult> Generate(string username)
    {
        Console.WriteLine($"GET request sent to api/{username}/games");
        var games = await _psnServiceClient.GetPlayerGames(username);
        return Ok(games);
    }
}