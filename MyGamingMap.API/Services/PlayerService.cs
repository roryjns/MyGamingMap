using MyGamingMap.API.Models.DTOs;
using System.Diagnostics;
using System.Text.Json;
using MyGamingMap.API.Exceptions;

namespace MyGamingMap.API.Services;

public class PlayerService(PSNService psnService, IGDBService igdbService, MapService mapService)
{
    private readonly PSNService psnService = psnService;
    private readonly IGDBService igdbService = igdbService;
    private readonly MapService mapService = mapService;

    public async Task<Analytics?> GetAnalytics(string username, CancellationToken ct = default)
    {
        var totalStopwatch = Stopwatch.StartNew();

        var stopwatch = Stopwatch.StartNew();
        var playerResult = await psnService.GetPlayerGames(username);
        ThrowIfPSNFailed(playerResult);
        stopwatch.Stop();
        Console.WriteLine($"PSNService.GetPlayerGames: {stopwatch.ElapsedMilliseconds} ms");

        if (!playerResult.Found) throw new PlayerNotFoundException();

        if (playerResult.Games.Count == 0) throw new NoGamesFoundException();

        stopwatch.Restart();
        var enrichedGames = await igdbService.GetEnrichedGames(playerResult.Games);
        stopwatch.Stop();
        Console.WriteLine($"IGDBService.GetIGDBGames: {stopwatch.ElapsedMilliseconds} ms");

        var distinctGames = EnrichedGameHelper.MergeByConceptId(enrichedGames);

        // Start the tier list computation before awaiting the map.
        // CalculateReviewRatingAnalytics is synchronous, so wrap in Task.Run
        // to move it off the request thread and run it concurrently with
        // the map's async LLM work.
        var tierListTask = Task.Run(() =>
            IGDBAnalyticsService
                .CalculateReviewRatingAnalytics(enrichedGames)
                .TierList,
            ct);

        // Map runs asynchronously; its LLM calls dominate the wait time.
        stopwatch.Restart();
        var map = await mapService.GetMapAsync(enrichedGames, distinctGames, ct);
        stopwatch.Stop();
        Console.WriteLine($"MapService.GetMapAsync: {stopwatch.ElapsedMilliseconds} ms");

        // Tier list should already be complete (or nearly so) by now.
        stopwatch.Restart();
        var tierList = await tierListTask;
        stopwatch.Stop();
        Console.WriteLine($"TierList wait: {stopwatch.ElapsedMilliseconds} ms");

        var analytics = new Analytics
        {
            Map = map,
            TierList = tierList,
        };

        totalStopwatch.Stop();
        Console.WriteLine($"Total GetAnalytics execution: {totalStopwatch.ElapsedMilliseconds} ms");
        return analytics;
    }

    public async Task<Map> GetMapAsync(string username, CancellationToken ct = default)
    {
        var playerResult = await psnService.GetPlayerGames(username);

        ThrowIfPSNFailed(playerResult);

        if (!playerResult.Found) throw new PlayerNotFoundException();

        if (playerResult.Games.Count == 0) throw new NoGamesFoundException();

        var enrichedGames = await igdbService.GetEnrichedGames(playerResult.Games);
        var distinctGames = EnrichedGameHelper.MergeByConceptId(enrichedGames);

        var stopwatch = Stopwatch.StartNew();
        var map = await mapService.GetMapAsync(enrichedGames, distinctGames, ct);
        stopwatch.Stop();
        Console.WriteLine($"Total GetMap execution: {stopwatch.ElapsedMilliseconds} ms");

        return map;
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
            await File.WriteAllTextAsync(resultsFile, string.Empty);
            Console.WriteLine("Starting new benchmark — previous results cleared.");
        }

        int successfulProfiles = 0;

        for (int i = startIndex; successfulProfiles < 5000; i++)
        {
            var user = usernames[i];
            var playerResult = await psnService.GetPlayerGames(user);

            ThrowIfPSNFailed(playerResult);

            if (!playerResult.Found || playerResult.Games.Count == 0) continue;

            var result = await igdbService.GetIGDBGamesBenchmark(playerResult.Games);

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

    public async Task RefreshNPSSO(string npsso)
    {
        await psnService.RefreshNPSSO(npsso);
    }

    private static void ThrowIfPSNFailed(PlayerGamesResult playerResult)
    {
        if (playerResult.Found) return;

        if (playerResult.Auth != null && playerResult.Auth == false) throw new PSNAuthenticationFailed();

        throw new PlayerNotFoundException();
    }
}