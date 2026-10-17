namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public sealed class SingleDayGamesRule : GameListRule
{
    protected override string EntityKey => "single day games";

    protected override IReadOnlyList<EnrichedPlayerGame> Games(EraAnalytics era) =>
        era.SingleDayGames;   // add this property to EraAnalytics, or compute inline

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