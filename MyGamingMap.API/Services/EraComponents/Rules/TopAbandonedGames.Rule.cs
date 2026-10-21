namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public sealed class TopAbandonedGamesRule : IEraComponentRule
{
    public string DataSource => "abandoned-games";

    public double Strength(EraAnalytics era) =>
        Math.Min(era.NewGameDroughts.DurationDays / 365.0, 1);

    private const int CalloutMax = 4;
    private const int CountdownMax = 10;

    public bool IsEligible(EraAnalytics era)
    {
        var count = era.AbandonedGames.Count;

        if (count == 0) { Log(era, count, "ineligible"); return false; }

        var decision = count <= CalloutMax
            ? $"callout"
            : $"countdown";

        Log(era, count, decision);
        return true;
    }

    public EraComponent Build(EraAnalytics era)
    {
        var all = era.AbandonedGames.OrderByDescending(a => a.DaysSinceLastPlayed).ToList();

        return all.Count <= CalloutMax
            ? BuildCallout(all)
            : BuildCountdown(all);
    }

    private static CalloutComponent BuildCallout(IReadOnlyList<AbandonedGame> all)
    {
        var entries = all.Select(a => new CalloutEntry
        {
            Label = a.Game.PlayerGame.Name,
            Game = a.Game,
            Value = a.DaysSinceLastPlayed,
            Unit = "days",
        }).ToList();

        return new CalloutComponent
        {
            Basis = ComponentBasis.AbandonedGames,
            Description = "Games you left behind.",
            Entries = entries,
        };
    }

    private static CountdownComponent BuildCountdown(IReadOnlyList<AbandonedGame> all)
    {
        var entries = all
            .Take(CountdownMax)
            .Select((a, idx) => new CountdownEntry
            {
                Rank = idx + 1,
                Label = a.Game.PlayerGame.Name,
                Game = a.Game,
                Value = a.DaysSinceLastPlayed,
                Unit = "days",
                Share = null,
            })
            .ToList();

        return new CountdownComponent
        {
            Basis = ComponentBasis.AbandonedGames,
            Description = "Games you left behind.",
            Entries = entries,
        };
    }

    private static string FormatAbandonmentAge(int days)
    {
        var months = days / 30.0;
        return months >= 12
            ? $"{months / 12:F1} years ago"
            : $"{months:F0} months ago";
    }

    private void Log(EraAnalytics era, int count, string decision)
    {
        Console.WriteLine(
            $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: " +
            $"count={count} -> {decision}");
    }
}

public sealed class AbandonedQuizRule : SpotTheOneQuizRule
{
    protected override string EntityKey => "abandoned-games";
    protected override string Question => "Which game did you abandon?";

    protected override IReadOnlyList<EnrichedPlayerGame> Accepted(EraAnalytics era) =>
        [.. era.AbandonedGames.Select(a => a.Game)];

    protected override bool NearMiss(EraAnalytics era, EnrichedPlayerGame g)
    {
        var pg = g.PlayerGame;
        if (pg.LastPlayed is null) return false;

        var daysSince = (DateTime.Now - pg.LastPlayed.Value).TotalDays;
        if (daysSince < PSNAnalyticsService.MinimumDaysSincePlayedForAbandoned) return false;
        if (!pg.PlayHours.HasValue) return false;
        if (pg.PlayHours.Value < PSNAnalyticsService.MinimumHoursPlayedForAbandoned) return false;
        return pg.TrophyData.All(t =>
            t.Progress < PSNAnalyticsService.MaximumTrophyProgressForAbandoned);
    }

    protected override double Proximity(EnrichedPlayerGame g)
    {
        var days = (DateTime.Now - g.PlayerGame.LastPlayed!.Value).TotalDays;
        return days;
    }
}