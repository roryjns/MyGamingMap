namespace MyGamingMap.API.Models;

// The player's PSN data paired with public IGDB data
// Passed to the analytics service for processing
// Not stored because it contains only duplicated, intermediary information
public class EnrichedPlayerGame
{
    public PlayerGame PlayerGame { get; set; } = null!;

    public IGDBGame IGDBGame { get; set; } = null!;
}