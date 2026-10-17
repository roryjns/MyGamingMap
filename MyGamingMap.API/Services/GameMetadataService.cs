using MyGamingMap.API.Models.DTOs;

namespace MyGamingMap.API.Services;

public class GameMetadataService
{
    public static DateOnly? GetReleaseDate(EnrichedPlayerGame game)
    {
        var releaseDates = game.IGDBGame?.ReleaseDates;

        if (releaseDates == null || releaseDates.Count == 0)
            return null;

        var firstPlayed = game.PlayerGame.FirstPlayed?.Date;

        // First prioritise the player's platform.
        var platformDates = releaseDates
            .Where(r =>
                r.Date.HasValue &&
                string.Equals(
                    r.Platform,
                    game.PlayerGame.Platform,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        // If there are no release dates for that platform,
        // fall back to release dates for all platforms.
        var candidates = platformDates.Count > 0
            ? platformDates
            : [.. releaseDates.Where(r => r.Date.HasValue)];

        if (candidates.Count == 0)
            return null;

        // Prefer a release date matching when the player first played.
        if (firstPlayed.HasValue)
        {
            var firstPlayedDate = DateOnly.FromDateTime(firstPlayed.Value);

            var exactMatch = candidates
                .Where(r => r.Date!.Value == firstPlayedDate)
                .ToList();

            if (exactMatch.Count > 0)
            {
                var worldwideExactMatch = exactMatch
                    .Where(r =>
                        string.Equals(
                            r.Region,
                            "worldwide",
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return (worldwideExactMatch.Count > 0
                        ? worldwideExactMatch
                        : exactMatch)
                    .First()
                    .Date;
            }

            // Don't allow a release date after the player's first play.
            candidates = [.. candidates.Where(r => r.Date!.Value < firstPlayedDate)];

            if (candidates.Count == 0)
                return null;
        }

        // Prefer worldwide when multiple valid dates remain.
        var worldwide = candidates
            .Where(r =>
                string.Equals(
                    r.Region,
                    "worldwide",
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (worldwide.Count > 0)
            candidates = worldwide;

        // Choose the earliest valid release date.
        return candidates
            .OrderBy(r => r.Date)
            .First()
            .Date;
    }
}