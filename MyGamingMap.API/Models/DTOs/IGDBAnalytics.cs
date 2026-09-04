namespace MyGamingMap.API.Models.DTOs;

public class IGDB_Analytics
{
    public Summary Summary { get; set; } = new();
    public TasteProfile TasteProfile { get; set; } = new();
    public List<FranchiseAnalytic> FranchiseAnalytics { get; set; } = [];
    public List<GameEngineAnalytic> GameEngineAnalytics { get; set; } = [];
    public List<GameModeAnalytic> GameModeAnalytics { get; set; } = [];
    public List<GenreAnalytic> GenreAnalytics { get; set; } = [];
    public List<ThemeAnalytic> ThemeAnalytics { get; set; } = [];
    public List<CompanyAnalytic> DeveloperAnalytics { get; set; } = [];
    public List<CompanyAnalytic> PublisherAnalytics { get; set; } = [];
    public AgeRatingAnalytics? AgeRatingAnalytics { get; set; } = new();
    public ReviewRatingAnalytics ReviewRatingAnalytics { get; set; } = new();
    public ReleaseDateAnalytics ReleaseDateAnalytics { get; set; } = new();
}

public class CategoryAnalytic
{
    public required string Name { get; set; }

    public int GamesPlayed { get; set; }
    public double HoursPlayed { get; set; }
    public int SessionsPlayed { get; set; }

    public double AverageHoursPerGame { get; set; }
    public double MedianHoursPerGame { get; set; }

    public double AverageSessionsPerGame { get; set; }
    public double MedianSessionsPerGame { get; set; }

    public double AverageSessionLength { get; set; }

    public double TotalCompletion { get; set; } // total trophies earned/total defined trophies (not including platinums)
    public double AverageCompletion { get; set; } // average progress across all games where progress != null and > 0
    public int GamesCompleted { get; set; } // games where progress = 100 and/or platinums earned = 1
    public int PlatinumsEarned { get; set; }
    public int PlatinumsAvailable { get; set; }
    public double PlatinumRate { get; set; } // games where platinums earned = 1/games where a platinum exists

    public double Breadth { get; set; }
    public double Investment { get; set; }
    public double CategoryRelevance { get; set; }

    public List<EnrichedPlayerGame> Games { get; set; } = [];
}

public class FranchiseAnalytic : CategoryAnalytic
{
    public int? FirstPlayedYear { get; set; }
    public int? LastPlayedYear { get; set; }

    public List<GamesStartedByYear> GamesStartedPerYear { get; set; } = [];
    public List<MostPlayedGame> MostPlayedGames { get; set; } = [];
}

public class GameEngineAnalytic : CategoryAnalytic { }

public class GameModeAnalytic : CategoryAnalytic
{
    public List<GamesStartedByYear> GamesStartedPerYear { get; set; } = [];
}

public class GenreAnalytic : CategoryAnalytic
{
    public List<GamesStartedByYear> GamesStartedPerYear { get; set; } = [];
    public List<MostPlayedGame> MostPlayedGames { get; set; } = [];
}

public class ThemeAnalytic : CategoryAnalytic
{
    public List<GamesStartedByYear> GamesStartedPerYear { get; set; } = [];
    public List<MostPlayedGame> MostPlayedGames { get; set; } = [];
}

public class CompanyAnalytic : CategoryAnalytic
{
    public int? FirstPlayedYear { get; set; }
    public int? LastPlayedYear { get; set; }
}

public class AgeRatingAnalytics
{
    public double AveragePEGIRating { get; set; }
    public double PlaytimeWeightedAgeRating { get; set; }
    public List<AgeRatingAnalytic> ESRBRatingAnalytics { get; set; } = [];
    public List<AgeRatingAnalytic> PEGIRatingAnalytics { get; set; } = [];
}

public class AgeRatingAnalytic : CategoryAnalytic
{
    public List<GamesStartedByYear> GamesStartedPerYear { get; set; } = [];
}

public class ReviewRatingAnalytics
{
    public double AverageReviewRating { get; set; }
    public List<ReviewRatingTier> RatingTiers { get; set; } = [];
    public List<EnrichedPlayerGame> HighestRatedGames { get; set; } = [];
    public List<EnrichedPlayerGame> LowestRatedGames { get; set; } = [];
    public List<EnrichedPlayerGame> HighRatingLowPlaytime { get; set; } = [];
    public List<EnrichedPlayerGame> LowRatingHighPlaytime { get; set; } = [];
}

public class ReviewRatingTier : CategoryAnalytic { }

public class ReleaseDateAnalytics
{
    public double GamingAge { get; set; }
    public DateOnly AverageReleaseDate { get; set; }
    public DateOnly PlaytimeWeightedAverageReleaseDate { get; set; }
    public double AverageReleaseToFirstPlayedTimeDays { get; set; }
    public List<ReleaseYearAnalytic> ReleaseYearAnalytics { get; set; } = [];
    public List<ReleaseGapGame> PlayedSoonAfterRelease { get; set; } = [];
    public List<ReleaseGapGame> PlayedLongAfterRelease { get; set; } = [];
}

public class ReleaseYearAnalytic : CategoryAnalytic
{
    public List<GamesStartedByYear> GamesStartedPerYear { get; set; } = [];
}

public class ReleaseGapGame
{
    public required EnrichedPlayerGame Game { get; set; }
    public DateOnly ReleaseDate { get; set; }
    public double DaysAfterRelease { get; set; }
}

public class Summary
{
    public int FranchiseCount { get; set; }
    public int GameEngineCount { get; set; }
    public int GenreCount { get; set; }
    public int ThemeCount { get; set; }
    public int DeveloperCount { get; set; }
    public int PublisherCount { get; set; }
}

public class TasteProfile
{
    public TasteProfileCategory Genres { get; set; } = new();
    public TasteProfileCategory Themes { get; set; } = new();
    public TasteProfileCategory GameModes { get; set; } = new(); // Play style e.g. mostly singleplayer but dabbles in multiplayer
    public TasteProfileCategory ESRBRating { get; set; } = new();
    public TasteProfileCategory PEGIRating { get; set; } = new();
    public List<string> UnexploredGenres { get; set; } = [];
    public List<string> UnexploredThemes { get; set; } = [];
}

public class TasteProfileCategory
{
    public string Description { get; set; } = string.Empty;
    public double Coverage { get; set; }
    public List<TastePreference> Evidence { get; set; } = [];
}

public class TastePreference
{
    public string Name { get; set; } = string.Empty;
    public double Relevance { get; set; }
}