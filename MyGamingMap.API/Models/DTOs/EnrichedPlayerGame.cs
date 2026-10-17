namespace MyGamingMap.API.Models.DTOs;

// The player's PSN data paired with IGDB data
// Passed to the analytics service for processing
public class EnrichedPlayerGame
{
    public PlayerGame PlayerGame { get; set; } = null!;

    public IGDBGame? IGDBGame { get; set; } = null!;
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