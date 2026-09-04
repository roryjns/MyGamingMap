namespace MyGamingMap.API.Models.DTOs;

public class PSNAnalytics
{
    public required ActivityAnalytics ActivityAnalytics { get; set; }
    public required TrophyAnalytics TrophyAnalytics { get; set; }
}

public class ActivityAnalytics
{
    public int TotalGamesPlayed { get; set; }
    public int UniqueGamesPlayed { get; set; }
    public double TotalHoursPlayed { get; set; }
    public int TotalSessionsPlayed { get; set; }

    public double AverageHoursPerGame { get; set; }
    public double MedianHoursPerGame { get; set; }

    public double AverageHoursPerDay { get; set; }
    public double AverageSessionsPerGame { get; set; }
    public double MedianSessionsPerGame { get; set; }

    public double AverageSessionLength { get; set; }

    public DateTime? FirstDay { get; set; } // Earliest first played/trophy
    public DateTime? LastDay { get; set; } // Latest last played/trophy
    public int ActivitySpanDays { get; set; } // Last day - first day

    // Last played - first played
    public double AverageGameSpanDays { get; set; }
    public double MedianGameSpanDays { get; set; }

    public List<MostPlayedGame> MostPlayedGames { get; set; } = [];
    public List<Drought> NewGameDroughts { get; set; } = [];
    public List<GamesStartedByYear> GamesStartedPerYear { get; set; } = [];
    public List<AbandonedGame> AbandonedGames { get; set; } = [];
    public List<EnrichedPlayerGame> LongestRunningGames { get; set; } = []; // Largest GameSpanDays
    public List<EnrichedPlayerGame> SingleDayGames { get; set; } = [];

    public int PS3GamesPlayed { get; set; }
    public int PSVitaGamesPlayed { get; set; }
    public PlatformPlaytime PS4 { get; set; } = new();
    public PlatformPlaytime PS5 { get; set; } = new();
}

public class AbandonedGame
{
    public EnrichedPlayerGame Game { get; set; } = null!;
    public int DaysSinceLastPlayed { get; set; }
    public double TrophyProgress { get; set; }
}

public class PlatformPlaytime
{
    public int GamesPlayed { get; set; }
    public double HoursPlayed { get; set; }
    public int SessionsPlayed { get; set; }

    public double AverageHoursPerGame { get; set; }
    public double MedianHoursPerGame { get; set; }

    public double AverageSessionsPerGame { get; set; }
    public double MedianSessionsPerGame { get; set; }
    
    public double AverageSessionLength { get; set; }
    public DateTime? FirstDay { get; set; } // Earliest first played/trophy
    public DateTime? LastDay { get; set; } // Latest last played/trophy
}

public class TrophyAnalytics
{
    public double TotalCompletion { get; set; } // total trophies earned/total defined trophies (not including platinums)
    public double AverageCompletion { get; set; } // average progress across all games where progress != null and > 0
    public int GamesCompleted { get; set; } // games where progress = 100 and/or platinums earned = 1
    public int PlatinumsEarned { get; set; }
    public int PlatinumsAvailable { get; set; }
    public double PlatinumRate { get; set; } // games where platinums earned = 1/games where a platinum exists
    public PlatFormTrophies PS3 { get; set; } = new();
    public PlatFormTrophies PSVita { get; set; } = new();
    public PlatFormTrophies PS4 { get; set; } = new();
    public PlatFormTrophies PS5 { get; set; } = new();
}

public class PlatFormTrophies
{
    public double TotalCompletion { get; set; }
    public double AverageCompletion { get; set; }
    public int GamesCompleted { get; set; }
    public int PlatinumsEarned { get; set; }
    public int PlatinumsAvailable { get; set; }
    public double PlatinumRate { get; set; }
}