namespace MyGamingMap.API.Models.DTOs;

public class IGDB_Analytics
{
    public List<FranchiseAnalytic>? FranchiseAnalytics { get; set; }
    public List<GameEngineAnalytic>? GameEngineAnalytics { get; set; }
    public List<GameModeAnalytic>? GameModeAnalytics { get; set; }
    public List<GenreAnalytic>? GenreAnalytics { get; set; }
    public List<ThemeAnalytic>? ThemeAnalytics { get; set; }
    public List<CompanyAnalytic>? DeveloperAnalytics { get; set; }
    public List<CompanyAnalytic>? PublisherAnalytics { get; set; }
    public AgeRatingAnalytics? AgeRatingAnalytics { get; set; }
    public ReviewRatingAnalytics? ReviewRatingAnalytics { get; set; }
    public ReleaseDateAnalytics? ReleaseDateAnalytics { get; set; }
}

public class CategoryAnalytic
{
    public required string Name { get; set; }

    public int GamesPlayed { get; set; }
    public double HoursPlayed { get; set; }
    public int SessionsPlayed { get; set; }
    public double PercentageOfTotalPlaytime { get; set; }


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
    public List<GamesStartedByYear> GamesStartedPerYear { get; set; } = [];
    public List<MostPlayedGame> MostPlayedGames { get; set; } = [];
    public int? FirstPlayedYear { get; set; }
    public int? LastPlayedYear { get; set; }
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
    public List<AgeRatingAnalytic> ESRBRatingAnalytics { get; set; } = [];
    public List<AgeRatingAnalytic> PEGIRatingAnalytics { get; set; } = [];
    public double AveragePEGIRating { get; set; }
    public double PlaytimeWeightedAgeRating { get; set; }
}

public class AgeRatingAnalytic : CategoryAnalytic
{
    public List<GamesStartedByYear> GamesStartedPerYear { get; set; } = [];
}

public class ReviewRatingAnalytics
{
    public List<ReviewRatingTier> RatingTiers { get; set; } = [];
    public List<EnrichedPlayerGame> HighestRatedGames { get; set; } = [];
    public List<EnrichedPlayerGame> LowestRatedGames { get; set; } = [];
    public List<EnrichedPlayerGame> HighRatingLowPlaytime { get; set; } = [];
    public List<EnrichedPlayerGame> LowRatingHighPlaytime { get; set; } = [];
    public double AverageReviewRating { get; set; }
    public double PlaytimeWeightedReviewRating { get; set; }
}

public class ReviewRatingTier : CategoryAnalytic { }

public class ReleaseDateAnalytics
{
    public List<ReleaseYearAnalytic> ReleaseYearAnalytics { get; set; } = [];
    public DateOnly AverageReleaseDate { get; set; }
    public double AverageReleaseToFirstPlayedTimeDays { get; set; }
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