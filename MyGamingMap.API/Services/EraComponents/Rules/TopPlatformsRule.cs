namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public sealed class TopPlatformsRule : IEraComponentRule
{
    private const double MinScore = 0.15;

    public string DataSource => "donut:platform-share";

    public double Strength(EraAnalytics era) =>
        IEraComponentRule.ScoreMetric(Shares(era), s => s.Value);

    public bool IsEligible(EraAnalytics era)
    {
        var shares = Shares(era);

        if (shares.Count == 0)
        {
            Log(era, "ineligible (no console games)");
            return false;
        }

        var score = IEraComponentRule.ScoreMetric(shares, s => s.Value);
        if (score < MinScore)
        {
            Log(era, $"ineligible (score {score:F3} < {MinScore:F3})");
            return false;
        }

        Log(era, $"donut, score={score:F3}");
        return true;
    }

    public EraComponent Build(EraAnalytics era)
    {
        var shares = Shares(era);

        return new DonutChartComponent
        {
            Basis = ComponentBasis.PlatformsByGamesPlayed,
            Description = Describe(shares),
            Entries = [.. shares
                .Select(s => new DonutChartEntry
                {
                    Label = s.Label,
                    Value = s.Value,
                    Unit = "games",
                })],
        };
    }

    private static List<(string Label, int Value)> Shares(EraAnalytics era)
    {
        var list = new List<(string, int)>(2);

        if (era.PS4GamesPlayed > 0)
            list.Add(("PS4", era.PS4GamesPlayed));

        if (era.PS5GamesPlayed > 0)
            list.Add(("PS5", era.PS5GamesPlayed));

        return list;
    }

    private static string Describe(List<(string Label, int Value)> shares)
    {
        var total = shares.Sum(s => s.Value);
        var (Label, Value) = shares.OrderByDescending(s => s.Value).First();
        var share = Value / (double)total;
        return $"You played {share:P0} of your console games on {Label} this era.";
    }

    private void Log(EraAnalytics era, string decision) =>
        Console.WriteLine(
            $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: {decision}");
}