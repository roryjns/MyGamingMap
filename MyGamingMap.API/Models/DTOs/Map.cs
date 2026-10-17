using System.Text.Json.Serialization;

namespace MyGamingMap.API.Models.DTOs;

public class Map
{
    public List<Era> Eras { get; set; } = [];
    public Overview? Overview { get; set; }
}

public class Era
{
    public required DateTime Start { get; init; }
    public required DateTime End { get; init; }
    public int MonthCount { get; set; }  // Used to scale size of era node
    public string Label { get; set; } = "";
    public EraSummary EraSummary { get; set; } = new();
    public List<EraComponent> Components { get; set; } = [];
}

public class EraSummary
{
    public string Summary { get; set; } = "";
    public List<EnrichedPlayerGame> Top3Games { get; set; } = [];
    public List<string> Top2Genres { get; set; } = [];
    public List<string> Top2Themes { get; set; } = [];
}

public class Overview
{
    public PSNAnalytics? PSN { get; set; }
    public IGDBAnalytics? IGDB { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CountdownComponent), "countdown")]
[JsonDerivedType(typeof(CalloutComponent), "callout")]
[JsonDerivedType(typeof(GridComponent), "grid")]
[JsonDerivedType(typeof(DonutChartComponent), "donut")]
[JsonDerivedType(typeof(BarChartComponent), "bar")]
public abstract class EraComponent
{
    public string? Description { get; init; }
}

public enum ComponentBasis
{
    // Callout
    LongTailStarts,
    LongTailEnds,
    NewGameDrought,

    // Callout and countdown
    GamesByHoursPlayed,
    AbandonedGames,
    DaysAfterRelease,
    DevelopersByHours,
    DevelopersByGames,
    PublishersByHours,
    PublishersByGames,
    FranchisesByHours,
    FranchisesByGames,

    // Callout and grid
    CompletedGames,
    SingleDayGames,

    // Donut chart
    PlatformsByGamesPlayed,
    GameModesByGamesPlayed,

    // Donut chart and bar chart
    GenresByGamesPlayed,
    ThemesByGamesPlayed,

    // Bar chart
    AgeRatingsByGamesPlayed,
    ReleaseYearsByGamesPlayed,
}

public sealed class CountdownComponent : EraComponent
{
    [JsonIgnore] public ComponentBasis Basis { get; init; }
    public required List<CountdownEntry> Entries { get; init; }
}

public sealed class CountdownEntry
{
    public required int Rank { get; init; }
    public required string Label { get; init; }
    public EnrichedPlayerGame? Game { get; init; }
    public string? CompanyImageUrl { get; init; }
    public double? Value { get; init; }
    public string? Unit { get; init; }
    public double? Share { get; init; }
}

public sealed class CalloutComponent : EraComponent
{
    [JsonIgnore] public ComponentBasis Basis { get; init; }
    public required List<CalloutEntry> Entries { get; init; }
}

public sealed class CalloutEntry
{
    public required string Label { get; init; }
    public EnrichedPlayerGame? Game { get; init; }
    public string? CompanyImageUrl { get; init; }
    public double? Value { get; init; }
    public string? Unit { get; init; }
}

public sealed class GridComponent : EraComponent
{
    [JsonIgnore] public ComponentBasis Basis { get; init; }
    public required List<GridEntry> Entries { get; init; }
}

public sealed class GridEntry
{
    public required string Label { get; init; }
    [JsonIgnore] public EnrichedPlayerGame? Game { get; init; }
    public bool PlatinumEarned { get; init; }
    public bool HundredPercentEarned { get; init; }
}

public sealed class DonutChartComponent : EraComponent
{
    [JsonIgnore] public ComponentBasis Basis { get; init; }
    public required List<DonutChartEntry> Entries { get; init; }
}

public sealed class DonutChartEntry
{
    public required string Label { get; init; }
    public int Value { get; init; }
    public string? Unit { get; init; }
}

public sealed class BarChartComponent : EraComponent
{
    [JsonIgnore] public ComponentBasis Basis { get; init; }
    public required List<BarChartEntry> Entries { get; init; }
}

public sealed class BarChartEntry
{
    public required string Label { get; init; }
    public int Value { get; init; }
    public string? Unit { get; init; }
}

public sealed class QuizComponent : EraComponent
{
    public required string Prompt { get; init; }
    public required List<QuizOption> Options { get; init; }
    public required int CorrectIndex { get; init; }
}

public sealed class QuizOption
{
    public required string Label { get; init; }
    public EnrichedPlayerGame? Game { get; init; }
}

public class EraAnalytics
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    public List<EnrichedPlayerGame> MergedGamesStarted { get; set; } = [];
    public int TotalGamesPlayed { get; set; }
    public int UniqueGamesPlayed { get; set; }
    public double TotalHoursPlayed { get; set; }
    public int TotalSessionsPlayed { get; set; }
    public double MedianHoursPerGame { get; set; }
    public double AverageSessionLength { get; set; }
    public double HoursPerMonth { get; set; }
    public int PS4GamesPlayed { get; set; }
    public int PS5GamesPlayed { get; set; }
    public List<MostPlayedGame> MostPlayedGames { get; set; } = [];
    public List<EnrichedPlayerGame> LongTailStarts { get; set; } = [];
    public List<EnrichedPlayerGame> LongTailEnds { get; set; } = [];
    public Drought NewGameDroughts { get; set; } = new();
    public List<EnrichedPlayerGame> CompletedGames { get; set; } = []; // Platinum earned or 100% earned
    public List<AbandonedGame> AbandonedGames { get; set; } = [];
    public List<EnrichedPlayerGame> SingleDayGames { get; set; } = [];
    public IGDBAnalytics IGDBAnalytics { get; set; } = new();
}