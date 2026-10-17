namespace MyGamingMap.API.Models.DTOs;

public class MonthlyAnalytic // Converted to a feature vector for segmentation. Not used for display.
{
    public required DateTime Month { get; set; }

    // --- Activity scalars ---

    public int ActiveGamesCount { get; set; }
    public int GamesStarted { get; set; }
    public int GamesEnded { get; set; }

    /// <summary>Sum of allocated monthly hours across active games.</summary>
    public double HoursPlayedEstimate { get; set; }

    // --- Lifecycle, hours-weighted, [0..1] ---

    /// <summary>Hours in games started within the last 2 months / HoursPlayedEstimate.</summary>
    public double NewGameHoursShare { get; set; }

    /// <summary>Hours in games started more than 6 months ago / HoursPlayedEstimate.</summary>
    public double LongTailHoursShare { get; set; }

    // --- Concentration, [0..1] ---

    /// <summary>Hours in the single most-played game / HoursPlayedEstimate.</summary>
    public double TopGameHoursShare { get; set; }

    /// <summary>Hours in the three most-played games / HoursPlayedEstimate.</summary>
    public double Top3GamesHoursShare { get; set; }

    /// <summary>Sum of (gameHours / totalHours)^2 across active games.
    /// 1.0 = one game consumed the month, 1/N = perfectly even spread.</summary>
    public double HoursConcentration { get; set; }

    // --- Completion, [0..1] where applicable ---

    /// <summary>Hours-weighted average trophy progress across active games, 0..1.</summary>
    public double AverageTrophyProgress { get; set; }

    /// <summary>Count of active games whose platinum has already been earned.</summary>
    public int PlatinumActiveCount { get; set; }

    /// <summary>PlatinumActiveCount / ActiveGamesCount, [0..1].</summary>
    public double PlatinumActiveRatio { get; set; }

    // --- Distributions ---

    public CategoryShare? PlatformShare { get; set; }
    public CategoryShare? FranchiseShare { get; set; }
    public CategoryShare? GameModeShare { get; set; }
    public CategoryShare? GenreShare { get; set; }
    public CategoryShare? ThemeShare { get; set; }
    public CategoryShare? DeveloperShare { get; set; }
    public CategoryShare? PublisherShare { get; set; }
    public CategoryShare? EsrbRatingShare { get; set; }
    public CategoryShare? PegiRatingShare { get; set; }
    public CategoryShare? ReleaseYearShare { get; set; }
}

public sealed class CategoryShare
{
    /// <summary>Normalized hours per label. Sums to 1.0 when non-empty.</summary>
    public required IReadOnlyDictionary<string, double> Shares { get; init; }

    /// <summary>HoursWithCategory / TotalHours, in [0, 1].
    /// TotalHours is the parent MonthlyAnalytic.HoursPlayedEstimate.</summary>
    public required double Coverage { get; init; }
}