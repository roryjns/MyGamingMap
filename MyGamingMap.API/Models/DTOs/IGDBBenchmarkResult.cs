namespace MyGamingMap.API.Models.DTOs;

public class IGDBBenchmarkResult
{
    // Current profile
    public int ProfileGames { get; init; }

    // Cache effectiveness
    public int DatabaseHits { get; init; }
    public int NameLookups { get; init; }
    public int ConceptIdLookups { get; init; }

    // Matching quality
    public int UnmatchedGames { get; init; }

    // Performance
    public TimeSpan ProcessingTime { get; init; }
}