namespace MyGamingMap.API.Services.EraSummaries;

public sealed class EraFacts
{
    public required DateTime Start { get; init; }
    public required DateTime End { get; init; }
    public required int MonthCount { get; init; }

    public required int DistinctGamesPlayed { get; init; }
    public int Platinums { get; init; }
    public required double EstimatedPlaytime { get; init; }
    public List<string> DominantGames { get; init; } = []; // >45% of estimated playtime

    /// Share of EstimatedPlaytime taken by DominantGames[0].
    /// 0.0–1.0. Null when DominantGames is empty.
    public double? DominantGameShare { get; init; }

    // Fraction of months in this era during which the dominant game was played.
    // 1.0 = every month. Null when DominantGames is empty.
    public double? DominantGameMonthCoverage { get; init; }

    public List<string> OtherTopGames { get; init; } = []; // >20% of estimated playtime
    public List<string> TopDevelopers { get; init; } = []; // >60% of estimated playtime
    public List<string> TopFranchises { get; init; } = []; // >40% of estimated playtime
    public required string EstimatedPlatformMix { get; init; } // e.g. PS4 only, PS5 mostly, balance of PS4 and PS5
    public string LongestNewGameDrought { get; init; } = "None"; // e.g. "95 days (Feb-May 2018)"
    public List<string> PlatinumGames { get; init; } = []; // top 5 by playtime
    public List<string> HundredPercentGames { get; init; } = []; // top 5 by playtime

    // top 2 games with ReviewRating >= 85 AND ReviewCount >= 500,
    public List<string> AcclaimedTitles { get; init; } = [];

    public List<string> TopAbandonedTitles { get; init; } = []; // top 2
}