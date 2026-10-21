namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public sealed class SingleDayGamesRule : GameListRule
{
    protected override string EntityKey => "single day games";

    protected override IReadOnlyList<EnrichedPlayerGame> Games(EraAnalytics era) =>
        era.SingleDayGames;   // add this property to EraAnalytics, or compute inline

    protected override int GridStrengthMax => 10;

    protected override ComponentBasis Basis => ComponentBasis.SingleDayGames;

    protected override string DescriptionFor(bool grid) => grid
        ? "Games you started and finished in one sitting."
        : "The games you knocked out in a day.";

    protected override (double? Value, string? Unit) CalloutStat(EnrichedPlayerGame g) =>
        (g.PlayerGame.PlayHours, "hours");

    protected override GridEntry ToGridEntry(EnrichedPlayerGame g)
    {
        var trophies = g.PlayerGame.TrophyData;
        return new GridEntry
        {
            Label = g.PlayerGame.Name,
            Game = g,
            PlatinumEarned = trophies.Sum(t => t.EarnedTrophies.Platinum) > 0,
            HundredPercentEarned = trophies.Any(t => t.Progress >= 100),
        };
    }
}

public sealed class SingleDayQuizRule : SpotTheOneQuizRule
{
    protected override string EntityKey => "single-day-games";
    protected override string Question => "Which game did you beat in a single day?";

    protected override IReadOnlyList<EnrichedPlayerGame> Accepted(EraAnalytics era) =>
        era.SingleDayGames;

    protected override bool NearMiss(EraAnalytics era, EnrichedPlayerGame g)
    {
        var pg = g.PlayerGame;
        if (pg.FirstPlayed is null || pg.LastPlayed is null) return false;
        if ((pg.LastPlayed.Value - pg.FirstPlayed.Value).TotalDays > 7) return false;
        if (!pg.PlayHours.HasValue) return false;

        var hoursOk = pg.PlayHours.Value >= PSNAnalyticsService.MinimumHoursPlayedForAbandoned;
        var progressOk = pg.TrophyData.Any(t =>
            t.Progress >= PSNAnalyticsService.MaximumTrophyProgressForAbandoned);
        return hoursOk || progressOk;
    }

    protected override double Proximity(EnrichedPlayerGame g)
    {
        var span = (g.PlayerGame.LastPlayed!.Value - g.PlayerGame.FirstPlayed!.Value).TotalDays;
        return 7 - span; // closer to 1 day = higher proximity
    }
}