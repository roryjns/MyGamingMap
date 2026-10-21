namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public sealed class TopGamesRule : IEraComponentRule
{
    public string DataSource => "top-games";

    private const int CalloutMax = 4;
    private const int CountdownMax = 10;

    public bool IsEligible(EraAnalytics era)
    {
        var count = era.MostPlayedGames.Count;

        if (count == 0)
        {
            Console.WriteLine(
                $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: " +
                $"count=0 -> ineligible (no games)");
            return false;
        }

        var decision = count <= CalloutMax
            ? $"callout"
            : $"countdown";

        Console.WriteLine(
            $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: " +
            $"count={count} -> {decision}");

        return true;
    }

    public EraComponent Build(EraAnalytics era)
    {
        var games = era.MostPlayedGames;

        return games.Count <= CalloutMax
            ? BuildCallout(games)
            : BuildCountdown(games);
    }

    private static CalloutComponent BuildCallout(
        IReadOnlyList<MostPlayedGame> games)
    {
        var entries = games
            .Select(mpg =>
            {
                var hours = mpg.Game.PlayerGame.PlayHours ?? 0;
                return new CalloutEntry
                {
                    Label = mpg.Game.PlayerGame.Name,
                    Game = mpg.Game,
                    Value = hours,
                    Unit = "hours",
                };
            })
            .ToList();

        return new CalloutComponent
        {
            Basis = ComponentBasis.GamesByHoursPlayed,
            Description = "The games that defined this era.",
            Entries = entries,
        };
    }

    private static CountdownComponent BuildCountdown(
        IReadOnlyList<MostPlayedGame> games)
    {
        var total = games.Sum(m => m.Game.PlayerGame.PlayHours ?? 0);

        var entries = games
            .Take(CountdownMax)
            .Select((mpg, idx) =>
            {
                var hours = mpg.Game.PlayerGame.PlayHours ?? 0;
                return new CountdownEntry
                {
                    Rank = idx + 1,
                    Label = mpg.Game.PlayerGame.Name,
                    Game = mpg.Game,
                    Value = hours,
                    Unit = "hours",
                    Share = total > 0 ? hours / total : null,
                };
            })
            .ToList();

        return new CountdownComponent
        {
            Basis = ComponentBasis.GamesByHoursPlayed,
            Description = "The games that defined this era.",
            Entries = entries,
        };
    }

    // Many games evenly spread = low strength, one dominant game = high strength
    public double Strength(EraAnalytics era)
    {
        var games = era.MostPlayedGames;
        if (games.Count < 2) return 0;

        var baseScore = IEraComponentRule.ScoreMetric(games, g => g.Game.PlayerGame.PlayHours ?? 0);
        return Math.Min(baseScore + 0.2, 1.0);   // top games is usually worth showing -> higher priority
    }
}