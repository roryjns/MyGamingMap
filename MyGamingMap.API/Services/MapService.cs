using MyGamingMap.API.Models.DTOs;
using System.Diagnostics;
using System.Text.Json;

namespace MyGamingMap.API.Services;

public class MapService(PSNService psnService, IGDBService igdbService, AnalyticsService analyticsService)
{
    private readonly PSNService psnService = psnService;
    private readonly IGDBService igdbService = igdbService;
    private readonly AnalyticsService analyticsService = analyticsService;

    public async Task<IGDBScrapeResult> GetMap(string username)
    {
        var totalStopwatch = Stopwatch.StartNew();

        var stopwatch = Stopwatch.StartNew();
        List<PlayerGame> games = await psnService.GetPlayerGames(username);
        stopwatch.Stop();
        Console.WriteLine($"PSNService.GetPlayerGames: {stopwatch.ElapsedMilliseconds} ms");

        stopwatch.Restart();
        var iGDBGames = await igdbService.GetIGDBGames(games);
        stopwatch.Stop();
        Console.WriteLine($"IGDBService.GetIGDBGames: {stopwatch.ElapsedMilliseconds} ms");

        totalStopwatch.Stop();
        Console.WriteLine($"Total GetMap execution: {totalStopwatch.ElapsedMilliseconds} ms");

        return iGDBGames;
        // var enrichedGames = icawait igdbService.EnrichPlayerGames(games);
        // var analytics = await analyticsService.GetAnalytics(enrichedGames);
        // map generation logic...
    }

    public async Task ScrapeIGDB()
    {
        var json = File.ReadAllText("psn_top_10000.json");
        List<string> usernames = JsonSerializer.Deserialize<List<string>>(json) ?? [];
        const string resultsFile = "igdb_scrape_results.jsonl";
        const string progressFile = "psn_scrape_progress.txt";

        int startIndex = 0;

        if (File.Exists(progressFile))
        {
            startIndex = int.Parse(File.ReadAllText(progressFile));
            Console.WriteLine($"Resuming from user {startIndex}");
        }

        for (int i = startIndex; i < 5005; i++)
        {
            var user = usernames[i];
            var games = await psnService.GetPlayerGames(user);
            var result = await igdbService.GetIGDBGames(games);

            // Write one JSON object per profile
            await File.AppendAllTextAsync(
                resultsFile,
                JsonSerializer.Serialize(new
                {
                    Username = user,
                    Timestamp = DateTime.UtcNow,
                    Result = result
                }) + Environment.NewLine
            );

            // Only advance after successful processing
            await File.WriteAllTextAsync(progressFile, (i + 1).ToString());
        }
    }
}