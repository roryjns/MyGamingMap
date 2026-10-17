namespace MyGamingMap.API.Services.EraComponents;

using MyGamingMap.API.Models.DTOs;

public interface IEraComponentRule
{
    /// Stable identifier for deduplication. Two rules with the same
    /// DataSource won't both appear in an era's component list.
    string DataSource { get; }

    /// Whether this rule can produce a component for this era.
    bool IsEligible(EraAnalytics era);

    /// Build the component. Called only if IsEligible returned true.
    EraComponent Build(EraAnalytics era);

    internal static EnrichedPlayerGame? FindRepresentativeGame(
        EraAnalytics ea,
        string? developer = null,
        string? publisher = null,
        string? franchise = null)
    {
        return ea.MergedGamesStarted
            .Where(g =>
        {
            var igdb = g.IGDBGame;
            if (igdb is null) return false;

            if (developer is not null &&
                igdb.Developers.Contains(developer, StringComparer.OrdinalIgnoreCase))
                return true;

            if (publisher is not null &&
                igdb.Publishers.Contains(publisher, StringComparer.OrdinalIgnoreCase))
                return true;

            if (franchise is not null &&
                igdb.Franchises.Contains(franchise, StringComparer.OrdinalIgnoreCase))
                return true;

            return false;
        })
        .OrderByDescending(g => OverlapWithEra(g, ea.Start, ea.End))
        .ThenByDescending(g => g.PlayerGame.PlayHours)
        .FirstOrDefault();
    }

    private static TimeSpan OverlapWithEra(
    EnrichedPlayerGame game,
    DateTime eraStart,
    DateTime eraEndExclusive)
    {
        var fp = game.PlayerGame.FirstPlayed;
        var lp = game.PlayerGame.LastPlayed;

        if (fp is null || lp is null) return TimeSpan.Zero;

        var overlapStart = fp.Value > eraStart ? fp.Value : eraStart;
        var overlapEnd = lp.Value < eraEndExclusive ? lp.Value : eraEndExclusive;

        return overlapEnd > overlapStart
            ? overlapEnd - overlapStart
            : TimeSpan.Zero;
    }

    internal static double ScoreMetric<T>(
    IEnumerable<T> entities,
    Func<T, double> valueSelector)
    {
        var values = entities
            .Select(valueSelector)
            .Where(v => v > 0)
            .OrderByDescending(v => v)
            .ToList();

        if (values.Count < 2) return 0;

        var total = values.Sum();
        if (total <= 0) return 0;

        var shares = values.Select(v => v / total).ToList();

        // Normalized HHI
        var hhi = shares.Sum(s => s * s);
        var n = shares.Count;
        var normalizedHhi = (hhi - 1.0 / n) / (1.0 - 1.0 / n);

        // Top-vs-second gap
        var gap = (values[0] - values[1]) / values[0];

        return 0.6 * normalizedHhi + 0.4 * gap;
    }

    public static double SpanDays(EnrichedPlayerGame g)
    {
        var fp = g.PlayerGame.FirstPlayed;
        var lp = g.PlayerGame.LastPlayed;
        if (!fp.HasValue || !lp.HasValue) return 0;
        return (lp.Value - fp.Value).TotalDays;
    }
}