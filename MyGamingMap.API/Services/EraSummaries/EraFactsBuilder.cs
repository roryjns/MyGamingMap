using System.Text;
using MyGamingMap.API.Models.DTOs;

namespace MyGamingMap.API.Services.EraSummaries;

public static class EraFactsBuilder
{
    // Share thresholds, all against era.TotalHoursPlayed.
    private const double DominantGameShare = 0.40;
    private const double OtherTopGameShare = 0.15;
    private const double TopDeveloperShare = 0.30;
    private const double TopFranchiseShare = 0.18;

    // Completion / quality / abandonment caps.
    private const int MaxPlatinumGames = 5;
    private const int MaxHundredPercentGames = 5;
    private const int MaxAcclaimedTitles = 2;
    private const int MaxAbandonedTitles = 2;
    private const int AcclaimedMinRating = 85;

    // Eras shorter than this are too short for secondary signals to
    // be meaningful (droughts, "other" games, summed category shares).
    private const int MinMonthsForSecondary = 6;

    public static EraFacts Build(EraAnalytics era)
    {
        var monthCount = MonthsBetween(era.Start, era.End);
        var totalHours = era.TotalHoursPlayed;    // ← the single denominator
        var longEnough = monthCount >= MinMonthsForSecondary;

        // MostPlayedGames already carries PercentageOfTotalPlaytime,
        // which is the game's share of era.TotalHoursPlayed. Use it
        // directly so the arithmetic matches the rest of the fact set.
        var ranked = era.MostPlayedGames
            .Where(g => g.Game?.PlayerGame is not null && g.PercentageOfTotalPlaytime > 0)
            .OrderByDescending(g => g.PercentageOfTotalPlaytime)
            .ToList();

        var dominantGames = longEnough
            ? ranked
                .Where(g => g.PercentageOfTotalPlaytime >= DominantGameShare * 100.0)
                .Select(g => g.Game.PlayerGame.Name)
                .ToList()
            : [];

        double? dominantShare = null;
        double? dominantCoverage = null;

        if (dominantGames.Count > 0)
        {
            // The dominant game is ranked[0]; its percentage is already
            // era-scoped (share of era.TotalHoursPlayed).
            dominantShare = ranked[0].PercentageOfTotalPlaytime / 100.0;

            var domGame = ranked[0].Game!.PlayerGame;

            // Coverage: count era months that intersect the game's
            // [FirstPlayed, LastPlayed] window. A month is "covered" if
            // the game was played at any point in it. Games without
            // FirstPlayed/LastPlayed can't be assessed → null.
            if (domGame.FirstPlayed is not null && domGame.LastPlayed is not null)
            {
                dominantCoverage = ComputeMonthCoverage(
                    era.Start, era.End, domGame.FirstPlayed.Value, domGame.LastPlayed.Value);
            }
        }

        var otherTopGames = longEnough
            ? ranked
                .Where(g =>
                {
                    var pct = g.PercentageOfTotalPlaytime;
                    return pct >= OtherTopGameShare * 100.0
                        && pct < DominantGameShare * 100.0;
                })
                .Select(g => g.Game.PlayerGame.Name)
                .ToList()
            : [];

        var topDevelopers = longEnough
            ? TopCategoryByShare(
                era.IGDBAnalytics?.DeveloperAnalytics,
                totalHours, TopDeveloperShare)
            : [];

        var topFranchises = longEnough
            ? TopCategoryByShare(
                era.IGDBAnalytics?.FranchiseAnalytics,
                totalHours, TopFranchiseShare)
            : [];

        var completedOrdered = era.CompletedGames
            .OrderByDescending(g => g.PlayerGame.PlayHours ?? 0)
            .ToList();

        var platinumEarners = completedOrdered
            .Where(HasPlatinum)
            .ToList();

        var nonPlatinumCompletions = completedOrdered
            .Where(g => !HasPlatinum(g) && IsHundredPercent(g))
            .ToList();

        var platinumGames = platinumEarners
            .GroupBy(g => g.PlayerGame.Name, StringComparer.Ordinal)
            .Select(grp => grp.OrderByDescending(g => g.PlayerGame.PlayHours ?? 0).First())
            .Take(MaxPlatinumGames)
            .Select(g => g.PlayerGame.Name)
            .ToList();

        var hundredPercentGames = nonPlatinumCompletions
            .Select(g => g.PlayerGame.Name)
            .Take(MaxHundredPercentGames)
            .ToList();

        var platinums = platinumEarners.Count;

        var acclaimedTitles = era.MostPlayedGames
            .Where(g => (g.Game?.IGDBGame?.ReviewRating ?? 0) >= AcclaimedMinRating)
            .OrderByDescending(g => g.Game!.IGDBGame!.ReviewCount)
            .Select(g => g.Game!.PlayerGame.Name)
            .Take(MaxAcclaimedTitles)
            .ToList();

        var abandonedTitles = era.AbandonedGames
            .OrderByDescending(a => a.DaysSinceLastPlayed)
            .Select(a => a.Game.PlayerGame.Name)
            .Take(MaxAbandonedTitles)
            .ToList();

        var platformMix = ComputePlatformMix(era.PS4GamesPlayed, era.PS5GamesPlayed)
            ?? "unknown";

        var drought = longEnough && era.NewGameDroughts is { DurationDays: > 0 } d
            ? $"{d.DurationDays} days ({FormatRange(d.StartDate, d.EndDate)})"
            : "None";

        return new EraFacts
        {
            Start = era.Start,
            End = era.End,
            MonthCount = monthCount,

            DistinctGamesPlayed = era.MergedGamesStarted.Count,
            EstimatedPlaytime = totalHours,

            DominantGames = dominantGames,
            DominantGameShare = dominantShare,
            DominantGameMonthCoverage = dominantCoverage,
            OtherTopGames = otherTopGames,
            TopDevelopers = topDevelopers,
            TopFranchises = topFranchises,

            EstimatedPlatformMix = platformMix,
            LongestNewGameDrought = drought,

            PlatinumGames = platinumGames,
            HundredPercentGames = hundredPercentGames,
            AcclaimedTitles = acclaimedTitles,
            Platinums = platinums,

            TopAbandonedTitles = abandonedTitles,
        };
    }

    private static int MonthsBetween(DateTime start, DateTime end) =>
        (end.Year - start.Year) * 12 + end.Month - start.Month;

    private static bool HasPlatinum(EnrichedPlayerGame g) =>
            g.PlayerGame.TrophyData.Any(t => t.EarnedTrophies.Platinum > 0);

    private static bool IsHundredPercent(EnrichedPlayerGame g) =>
        g.PlayerGame.TrophyData.Any(t => t.Progress >= 100);

    /// Selects category labels whose own playtime exceeds
    /// <paramref name="minShare"/> of the era's total playtime.
    /// Because each label is tested individually, the caps derived from
    /// the threshold are tight: 2 × 60% &gt; 100%, so at most 1 developer
    /// can qualify; 3 × 40% &gt; 100%, so at most 2 franchises can qualify.
    private static List<string> TopCategoryByShare(
        IEnumerable<CategoryAnalytic>? analytics,
        double totalHours,
        double minShare
    )
    {
        if (analytics is null || totalHours <= 0) return [];

        return [.. analytics
            .Where(a => a.HoursPlayed > 0)
            .Select(a => (a.Name, Share: a.HoursPlayed / totalHours))
            .Where(x => x.Share >= minShare)
            .OrderByDescending(x => x.Share)
            .ThenBy(x => x.Name, StringComparer.Ordinal)  // tiebreak
            .Select(x => x.Name)];
    }

    private static string? ComputePlatformMix(int ps4, int ps5)
    {
        var total = ps4 + ps5;
        if (total == 0) return null;
        if (ps4 == 0) return "PS5 only";
        if (ps5 == 0) return "PS4 only";

        var ps5Share = ps5 / (double)total;

        return ps5Share switch
        {
            >= 0.9 => "PS5 only, essentially",
            >= 0.7 => "mostly PS5",
            >= 0.55 => "leaning PS5",
            > 0.45 and < 0.55 => "split evenly between PS4 and PS5",
            > 0.30 and <= 0.45 => "leaning PS4",
            > 0.10 and <= 0.30 => "mostly PS4",
            _ => "PS4 only, essentially",
        };
    }

    /// Fraction of the months in [eraStart, eraEnd] that intersect
    /// [playedFrom, playedTo]. A month is counted if the game was
    /// played at any point in it. Clamps the game's window to the era
    /// first, so a game spanning the whole era scores 1.0 regardless
    /// of how long it ran before/after.
    private static double ComputeMonthCoverage(
        DateTime eraStart, DateTime eraEnd,
        DateTime playedFrom, DateTime playedTo)
    {
        // Clamp to era bounds.
        var from = playedFrom < eraStart ? eraStart : playedFrom;
        var to = playedTo > eraEnd ? eraEnd : playedTo;
        if (from > to) return 0.0;

        var eraMonths = MonthsBetween(eraStart, eraEnd) + 1; // inclusive
        if (eraMonths <= 0) return 0.0;

        // Inclusive month span of the (clamped) window.
        var coveredMonths = MonthsBetween(
            new DateTime(from.Year, from.Month, 1),
            new DateTime(to.Year, to.Month, 1)) + 1;

        return Math.Min(1.0, coveredMonths / (double)eraMonths);
    }

    private static string FormatRange(DateTime start, DateTime end)
    {
        if (start.Year == end.Year)
            return $"{start:MMM}–{end:MMM yyyy}";
        return $"{start:MMM yyyy}–{end:MMM yyyy}";
    }

    public static string BuildBatchPrompt(IReadOnlyList<EraFacts> eras)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# {eras.Count} eras, in order");
        sb.AppendLine();

        // Pre-compute relative rankings across the batch.
        var gamesPerMonth = eras
            .Select(f => f.MonthCount > 0
                ? f.DistinctGamesPlayed / (double)f.MonthCount
                : 0.0)
            .ToList();

        var platinumsPerMonth = eras
        .Select(f => f.MonthCount > 0
            ? f.Platinums / (double)f.MonthCount
            : 0.0)
        .ToList();

        var playtimePerMonth = eras
            .Select(f => f.MonthCount > 0
                ? f.EstimatedPlaytime / f.MonthCount
                : 0.0)
            .ToList();

        for (var i = 0; i < eras.Count; i++)
        {
            sb.AppendLine($"## Era {i + 1}");
            sb.AppendLine();
            AppendEraFacts(sb, eras[i]);
            sb.AppendLine("### Relative to other eras in this set");
            sb.AppendLine($"- Games per month: {RankLabel(gamesPerMonth, i)}");
            sb.AppendLine($"- Platinums per month: {RankLabel(platinumsPerMonth, i)}");
            sb.AppendLine($"- Playtime per month: {RankLabel(playtimePerMonth, i)}");
            sb.AppendLine();
        }

        sb.AppendLine("Return one entry per era, keyed by the Start month above.");
        Console.WriteLine(sb);
        return sb.ToString();
    }

    private static string RankLabel(List<double> values, int index)
    {
        var sorted = values.OrderByDescending(v => v).ToList();
        var rank = sorted.FindIndex(v => Math.Abs(v - values[index]) < 1e-9) + 1;
        var total = sorted.Count;

        var ties = sorted.Count(v => Math.Abs(v - values[index]) < 1e-9);
        if (ties > 1) return $"tied for {Ordinal(rank)} of {total}";
        if (rank == 1) return "highest in the set";
        if (rank == total) return "lowest in the set";
        return $"{Ordinal(rank)} of {total}";
    }

    private static string Ordinal(int n) => n switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => $"{n}th",
    };

    private static void AppendEraFacts(StringBuilder sb, EraFacts f)
    {
        // ── Shape ─────────────────────────────────────────────────────
        sb.AppendLine("### Span");
        sb.AppendLine($"- {f.Start:MMMM yyyy} to {f.End:MMMM yyyy} ({f.MonthCount} months)");
        sb.AppendLine($"- {f.Platinums} platinums earned");
        sb.AppendLine($"- Platform: {f.EstimatedPlatformMix}");
        sb.AppendLine();

        if (f.DominantGames.Count > 0)
        {
            var game = f.DominantGames[0];
            var share = f.DominantGameShare is { } s ? $"{s:P0}" : "over 40%";
            sb.AppendLine("### Dominant game");
            sb.AppendLine($"- {game} ({share} of playtime)");

            if (f.DominantGameMonthCoverage is { } cov)
            {
                var continuity = cov switch
                {
                    >= 0.90 => "present in essentially every month (an ongoing habit, not a burst)",
                    >= 0.60 => "played across most of the era",
                    >= 0.30 => "played intermittently across the era",
                    _ => "a short, intense burst",
                };
                sb.AppendLine($"- {continuity}");
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("### Dominant game");
            sb.AppendLine("- None. No single game dominated your playtime; this era was diverse.");
            sb.AppendLine();
        }

        var otherLines = new List<string>();
        if (f.OtherTopGames.Count > 0)
            otherLines.Add($"- Meaningful share of playtime: {string.Join(", ", f.OtherTopGames)}");
        if (f.AcclaimedTitles.Count > 0)
            otherLines.Add($"- Widely acclaimed (high review score, many reviews): {string.Join(", ", f.AcclaimedTitles)}");

        if (otherLines.Count > 0)
        {
            sb.AppendLine("### Other notable games");
            foreach (var line in otherLines) sb.AppendLine(line);
            sb.AppendLine();
        }

        if (f.PlatinumGames.Count > 0 || f.HundredPercentGames.Count > 0)
        {
            sb.AppendLine("### Completion");
            if (f.PlatinumGames.Count > 0)
                sb.AppendLine($"- Platinumed: {string.Join(", ", f.PlatinumGames)}");
            if (f.HundredPercentGames.Count > 0)
                sb.AppendLine($"- 100% complete (no platinum): {string.Join(", ", f.HundredPercentGames)}");
            sb.AppendLine();
        }

        var identityLines = new List<string>();
        if (f.TopDevelopers.Count > 0)
            identityLines.Add($"- Dominant developer: {string.Join(", ", f.TopDevelopers)}");
        if (f.TopFranchises.Count > 0)
            identityLines.Add($"- Dominant franchise: {string.Join(", ", f.TopFranchises)}");

        if (identityLines.Count > 0)
        {
            sb.AppendLine("### Identity");
            foreach (var line in identityLines) sb.AppendLine(line);
            sb.AppendLine();
        }

        var hasDrought = !string.IsNullOrWhiteSpace(f.LongestNewGameDrought)
            && !string.Equals(f.LongestNewGameDrought, "None", StringComparison.Ordinal);

        if (hasDrought || f.TopAbandonedTitles.Count > 0)
        {
            sb.AppendLine("### Lulls and walkaways");
            if (hasDrought)
                sb.AppendLine($"- Longest gap between new games: {f.LongestNewGameDrought}");
            if (f.TopAbandonedTitles.Count > 0)
                sb.AppendLine($"- Left unfinished: {string.Join(", ", f.TopAbandonedTitles)}");
            sb.AppendLine();
        }
    }
}