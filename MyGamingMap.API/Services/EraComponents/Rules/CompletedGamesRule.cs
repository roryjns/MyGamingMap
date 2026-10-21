namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public sealed class CompletedGamesRule : GameListRule
{
    protected override string EntityKey => "completed games";

    protected override IReadOnlyList<EnrichedPlayerGame> Games(EraAnalytics era) =>
        era.CompletedGames;

    protected override int GridStrengthMax => 30;
    
    protected override ComponentBasis Basis => ComponentBasis.CompletedGames;

    protected override string DescriptionFor(bool grid) => grid
        ? "Every game you finished this era."
        : "The games you finished this era.";

    protected override (double? Value, string? Unit) CalloutStat(EnrichedPlayerGame g)
    {
        var trophies = g.PlayerGame.TrophyData;
        var platinum = trophies.Sum(t => t.EarnedTrophies.Platinum);
        if (platinum > 0) return (platinum, "platinum");
        var progress = trophies.Max(t => t.Progress);
        return (progress, "% complete");
    }

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

public sealed class CompletedQuizRule : SpotTheOneQuizRule
{
    protected override string EntityKey => "completed-games";
    protected override string Question => "Which game did you complete this era?";

    protected override IReadOnlyList<EnrichedPlayerGame> Accepted(EraAnalytics era) =>
        era.CompletedGames;

    protected override bool NearMiss(EraAnalytics era, EnrichedPlayerGame g)
    {
        if (g.PlayerGame.TrophyData is not { Count: > 0 }) return false;
        return g.PlayerGame.TrophyData.Any(t =>
            t.Progress >= 80 &&
            t.LastTrophyEarned.HasValue &&
            t.LastTrophyEarned.Value >= era.Start &&
            t.LastTrophyEarned.Value < era.End);
    }

    protected override double Proximity(EnrichedPlayerGame g) =>
        g.PlayerGame.TrophyData.Max(t => t.Progress);
}