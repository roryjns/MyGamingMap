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
    public required EnrichedPlayerGame Game { get; set; } = null!;
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

public static class EnrichedGameHelper
{
    public static List<EnrichedPlayerGame> MergeByConceptId(IEnumerable<EnrichedPlayerGame> games)
    {
        return [.. games
            .GroupBy(g => g.PlayerGame.ConceptId)
            .SelectMany(group =>
            {
                // Games without a ConceptId must remain separate.
                // Otherwise every null ConceptId would be merged together.
                if (!group.Key.HasValue)
                    return group;

                var gameList = group.ToList();

                var representative = gameList
                    .OrderByDescending(g => g.PlayerGame.PlayHours ?? 0)
                    .First();

                return
                [
                    new EnrichedPlayerGame
                    {
                        PlayerGame = new PlayerGame
                        {
                            TitleId = representative.PlayerGame.TitleId,
                            Name = representative.PlayerGame.Name,

                            Platform = string.Join(",",
                                gameList
                                    .Select(g => g.PlayerGame.Platform)
                                    .Where(p => !string.IsNullOrWhiteSpace(p))
                                    .Distinct()),

                            ConceptId = group.Key,

                            ImageUrl = representative.PlayerGame.ImageUrl,

                            // Recalculate aggregated gameplay data.
                            PlayHours = gameList.Sum(g =>
                                g.PlayerGame.PlayHours ?? 0),

                            PlayCount = gameList.Sum(g =>
                                g.PlayerGame.PlayCount ?? 0),

                            FirstPlayed = gameList
                                .Where(g => g.PlayerGame.FirstPlayed.HasValue)
                                .Min(g => g.PlayerGame.FirstPlayed),

                            LastPlayed = gameList
                                .Where(g => g.PlayerGame.LastPlayed.HasValue)
                                .Max(g => g.PlayerGame.LastPlayed),

                            TrophyData = gameList.Count > 1 ? [] : representative.PlayerGame.TrophyData,
                        },

                        // Keep the representative game's IGDB data.
                        IGDBGame = representative.IGDBGame
                    }
                ];
            })];
    }
}