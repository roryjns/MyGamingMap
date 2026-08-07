using Microsoft.AspNetCore.Mvc;
using MyGamingMap.API.Services;

namespace MyGamingMap.API.Controllers;

[ApiController]
[Route("api")]
public class PlayerController(MapService mapService) : ControllerBase
{
    private readonly MapService mapService = mapService;
    
    [HttpGet("{username}/map")]
    public async Task<IActionResult> GetMap(string username)
    {
        var map = await mapService.GetMap(username);
        return Ok(map);
    }

    [HttpPost("scrape-igdb")]
    public async Task<IActionResult> ScrapeIGDB()
    {
        await mapService.ScrapeIGDB();
        return Ok();
    }
}