using MyGamingMap.API.Models.DTOs;
using System.Diagnostics;
using System.Text.Json;

namespace MyGamingMap.API.Services;

public class PlayerService(PSNService psnService, IGDBService igdbService, AnalyticsService analyticsService)
{
    private readonly PSNService psnService = psnService;
    private readonly IGDBService igdbService = igdbService;
    private readonly AnalyticsService analyticsService = analyticsService;

    public async Task<Analytics> GetMap(string username)
    {
        var totalStopwatch = Stopwatch.StartNew();

        var stopwatch = Stopwatch.StartNew();
        var games = await psnService.GetPlayerGames(username);
        stopwatch.Stop();
        Console.WriteLine($"PSNService.GetPlayerGames: {stopwatch.ElapsedMilliseconds} ms");

        stopwatch.Restart();
        var enrichedGames = await igdbService.GetEnrichedGames(games);
        stopwatch.Stop();
        Console.WriteLine($"IGDBService.GetIGDBGames: {stopwatch.ElapsedMilliseconds} ms");

        stopwatch.Restart();
        var analytics = await analyticsService.GenerateAnalytics(enrichedGames);
        stopwatch.Stop();
        Console.WriteLine($"AnalyticsService.GenerateAnalytics: {stopwatch.ElapsedMilliseconds} ms");

        //stopwatch.Restart();
        //var map = await mapService.GenerateMap(analytics);
        //stopwatch.Stop();
        //Console.WriteLine($"MapService.GenerateMap: {stopwatch.ElapsedMilliseconds} ms");

        totalStopwatch.Stop();
        Console.WriteLine($"Total GetMap execution: {totalStopwatch.ElapsedMilliseconds} ms");
        return analytics;
        //return map;
    }

    public async Task BenchmarkTest()
    {
        var json = File.ReadAllText("psn_top_10000.json");
        List<string> usernames = JsonSerializer.Deserialize<List<string>>(json) ?? [];
        const string resultsFile = "benchmark_results.jsonl";
        const string progressFile = "benchmark_progress.txt";

        int startIndex = 0;

        if (File.Exists(progressFile))
        {
            startIndex = int.Parse(File.ReadAllText(progressFile));
            Console.WriteLine($"Resuming from user {startIndex}");
        }
        else
        {
            // Starting a new benchmark, so clear previous results
            await File.WriteAllTextAsync(resultsFile, string.Empty);
            Console.WriteLine("Starting new benchmark — previous results cleared.");
        }

        int successfulProfiles = 0;

        for (int i = startIndex; successfulProfiles < 5000; i++)
        {
            var user = usernames[i];
            var games = await psnService.GetPlayerGames(user);
            var result = await igdbService.GetIGDBGamesBenchmark(games);
            // eventually add analytics and map service to benchmark

            if (result.ProfileGames > 0)
            {
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

                successfulProfiles++;
            }

            // Only advance after successful processing
            await File.WriteAllTextAsync(progressFile, (i + 1).ToString());
        }

        File.Delete(progressFile);
        Console.WriteLine("Benchmark test complete");
    }
}