namespace MyGamingMap.API.Models.DTOs;

public class IGDBMatchingResult
{
    public List<EnrichedPlayerGame> EnrichedGames { get; set; } = [];

    public IGDBBenchmarkResult BenchmarkResult { get; set; } = null!;
}