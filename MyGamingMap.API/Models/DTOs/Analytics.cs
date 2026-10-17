namespace MyGamingMap.API.Models.DTOs;

public class Analytics
{
    public Map? Map { get; set; }
    public TierList? TierList { get; set; }
    //public HallOfFame? HallOfFame { get; set; }
}

// Classes shared by both analytics sub-services
public class TrophyGame
{
    public required EnrichedPlayerGame Game { get; init; }
    public required TrophyData Trophy { get; init; }
}

public class MostPlayedGame
{
    public required EnrichedPlayerGame Game { get; set; } = null!;
    public double PercentageOfTotalPlaytime { get; set; }
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