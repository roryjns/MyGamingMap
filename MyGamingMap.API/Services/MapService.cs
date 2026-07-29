using MyGamingMap.API.Models;
using System.Diagnostics;

namespace MyGamingMap.API.Services;

public class MapService(PSNService psnService, IGDBService igdbService, AnalyticsService analyticsService)
{
    private readonly PSNService psnService = psnService;
    private readonly IGDBService igdbService = igdbService;
    private readonly AnalyticsService analyticsService = analyticsService;

    public async Task<IEnumerable<IGDBGame?>> GetMap(string username)
    {
        var totalStopwatch = Stopwatch.StartNew();

        var stopwatch = Stopwatch.StartNew();
        List<PlayerGame> games = await psnService.GetPlayerGames(username);
        stopwatch.Stop();
        Console.WriteLine($"PSNService.GetPlayerGames: {stopwatch.ElapsedMilliseconds} ms");

        stopwatch.Restart();
        List<PlayerGame> firstTenGames = [.. games.Take(1000)];
        var igdbTest = await igdbService.GetIGDBGames(firstTenGames);
        stopwatch.Stop();
        Console.WriteLine($"IGDBService.GetIGDBGames: {stopwatch.ElapsedMilliseconds} ms");

        totalStopwatch.Stop();
        Console.WriteLine($"Total GetMap execution: {totalStopwatch.ElapsedMilliseconds} ms");

        return igdbTest;
        // var enrichedGames = await igdbService.EnrichPlayerGames(games);
        // var analytics = await analyticsService.GetAnalytics(enrichedGames);
        // map generation logic...
    }
}