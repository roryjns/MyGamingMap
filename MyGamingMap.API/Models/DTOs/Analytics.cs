namespace MyGamingMap.API.Models.DTOs;

// Sent to the map service to be converted to a map
public class Analytics
{
    public PSNAnalytics? PSN { get; set; }
    public IGDB_Analytics? IGDB { get; set; }
}

public class TrophyGame
{
    public required EnrichedPlayerGame Game { get; init; }
    public required TrophyData Trophy { get; init; }
}

// Classes shared by both analytics sub-services
public class MostPlayedGame
{
    public EnrichedPlayerGame Game { get; set; } = null!;
    public double HoursPlayed { get; set; }
    public double SessionsPlayed { get; set; }
    public double PercentageOfTotalPlaytime { get; set; }
}

public class Drought
{
    public int DurationDays { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public EnrichedPlayerGame? LastGameBefore { get; set; }
    public EnrichedPlayerGame? FirstGameAfter { get; set; }
}

public class GamesStartedByYear
{
    public int Year { get; set; }
    public int GamesStarted { get; set; }
}

public static class TrophyAnalyticsHelper
{
    public static double CalculateTotalCompletion(IEnumerable<TrophyData> trophies)
    {
        var trophyList = trophies.ToList();

        var totalDefined = trophyList.Sum(t =>
            t.DefinedTrophies.Bronze +
            t.DefinedTrophies.Silver +
            t.DefinedTrophies.Gold);

        var totalEarned = trophyList.Sum(t =>
            t.EarnedTrophies.Bronze +
            t.EarnedTrophies.Silver +
            t.EarnedTrophies.Gold);

        return totalDefined > 0
            ? (double)totalEarned / totalDefined * 100
            : 0;
    }

    public static string GetGameKey(TrophyGame trophyGame)
    {
        var game = trophyGame.Game;

        return game.PlayerGame.ConceptId?.ToString()
            ?? game.PlayerGame.TitleId
            ?? game.PlayerGame.Name;
    }
}