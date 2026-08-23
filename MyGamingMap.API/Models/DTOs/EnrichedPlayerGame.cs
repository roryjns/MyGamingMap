namespace MyGamingMap.API.Models.DTOs;

// The player's PSN data paired with IGDB data
// Passed to the analytics service for processing
public class EnrichedPlayerGame
{
    public PlayerGame PlayerGame { get; set; } = null!;

    public IGDBGame? IGDBGame { get; set; } = null!;
}