namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public sealed class TimelineRule : IEraComponentRule
{
    private const double MinGamesPerMonth = 1.0;
    private const int MaxGamesInAnyMonth = 10;
    private const int MinMonths = 5;
    private const int MaxMonths = 14;
    private const double MaxTopMonthShare = 0.5;   // no month > 50% of starts
    private const double MinActiveMonthShare = 0.5; // at least half the months have games

    public double Strength(EraAnalytics era)
    {
        var months = Months(era);
        if (months.Count == 0) return 0;

        var total = months.Sum(m => m.Games.Count);
        if (total == 0) return 0;

        var average = total / (double)months.Count;
        var density = Math.Min(average / MaxGamesInAnyMonth, 1);
        var activeShare = months.Count(m => m.Games.Count > 0) / (double)months.Count;

        // Timelines are visually unique — weight them slightly higher than
        // their raw density × coverage would suggest, and give a flat bonus
        // so a "just eligible" timeline can still compete.
        return Math.Min(density * activeShare + 0.35, 1.0);
    }

    public string DataSource => "timeline:games-started";

    public bool IsEligible(EraAnalytics era)
    {
        var months = Months(era);

        if (months.Count == 0)
        {
            Log(era, 0, "ineligible (no months)");
            return false;
        }

        if (months.Count < MinMonths)
        {
            Log(era, months.Count, $"ineligible ({months.Count} months < {MinMonths})");
            return false;
        }

        if (months.Count > MaxMonths)
        {
            Log(era, months.Count, $"ineligible ({months.Count} months > {MaxMonths})");
            return false;
        }

        var total = months.Sum(m => m.Games.Count);

        if (total == 0)
        {
            Log(era, months.Count, "ineligible (no games started)");
            return false;
        }

        var average = total / (double)months.Count;
        if (average <= MinGamesPerMonth)
        {
            Log(era, months.Count,
                $"ineligible (avg {average:F2}/mo <= {MinGamesPerMonth:F2})");
            return false;
        }

        var peak = months.Max(m => m.Games.Count);
        if (peak > MaxGamesInAnyMonth)
        {
            Log(era, months.Count,
                $"ineligible (peak {peak}/mo > {MaxGamesInAnyMonth})");
            return false;
        }

        var topShare = peak / (double)total;
        if (topShare > MaxTopMonthShare)
        {
            Log(era, months.Count,
                $"ineligible (top month {topShare:P0} > {MaxTopMonthShare:P0})");
            return false;
        }

        var activeMonths = months.Count(m => m.Games.Count > 0);
        var activeShare = activeMonths / (double)months.Count;
        if (activeShare < MinActiveMonthShare)
        {
            Log(era, months.Count,
                $"ineligible (only {activeMonths}/{months.Count} months active)");
            return false;
        }

        Log(era, months.Count,
            $"timeline (avg {average:F2}/mo, peak {peak}");

        return true;
    }

    public EraComponent Build(EraAnalytics era)
    {
        var months = Months(era);

        return new TimelineComponent
        {
            Basis = ComponentBasis.GamesStarted,
            Description = Describe(months),
            Entries = [.. months
                .Select(m => new TimelineEntry
                {
                    Month = m.Month.ToString("yyyy-MM"),
                    GamesStarted = m.Games,
                })],
        };
    }

    private static List<(DateTime Month, List<EnrichedPlayerGame> Games)> Months(EraAnalytics era)
    {
        var games = era.MergedGamesStarted;
        if (games is null || games.Count == 0)
            return [];

        var byMonth = games
            .Where(g => g.PlayerGame.FirstPlayed.HasValue)
            .GroupBy(g => new DateTime(
                g.PlayerGame.FirstPlayed!.Value.Year,
                g.PlayerGame.FirstPlayed!.Value.Month, 1))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.PlayerGame.FirstPlayed).ToList());

        var start = new DateTime(era.Start.Year, era.Start.Month, 1);
        var end = new DateTime(era.End.Year, era.End.Month, 1);

        var result = new List<(DateTime, List<EnrichedPlayerGame>)>();
        for (var m = start; m <= end; m = m.AddMonths(1))
            result.Add((m, byMonth.GetValueOrDefault(m) ?? []));

        return result;
    }

    private static string Describe(List<(DateTime Month, List<EnrichedPlayerGame> Games)> months)
    {
        var (Month, Games) = months.OrderByDescending(m => m.Games.Count).First();
        return $"You started the most games in {Month:MMM yyyy} ({Games.Count}).";
    }

    private void Log(EraAnalytics era, int count, string decision) =>
        Console.WriteLine(
            $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: " +
            $"months={count} -> {decision}");
}