namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public sealed class GameModeShareRule : IEraComponentRule
{
    private const double MinScore = 0.2;

    public string DataSource => "donut:game-mode-share";

    public bool IsEligible(EraAnalytics era)
    {
        var slices = Shares(era);

        if (slices.Count == 0)
        {
            Log(era, "ineligible (no game modes)");
            return false;
        }

        var score = IEraComponentRule.ScoreMetric(slices, s => s.Value);
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
        var slices = Shares(era);

        return new DonutChartComponent
        {
            Basis = ComponentBasis.GameModesByGamesPlayed,
            Description = Describe(slices),
            Entries = [.. slices
                .Select(s => new DonutChartEntry
                {
                    Label = s.Label,
                    Value = s.Value,
                    Unit = "games",
                })],
        };
    }

    private static List<(string Label, int Value)> Shares(EraAnalytics era) =>
        [.. era.IGDBAnalytics.GameModeAnalytics
            .Where(m => m.GamesPlayed > 0)
            .Select(m => (m.Name, m.GamesPlayed))];

    private static string Describe(List<(string Label, int Value)> slices)
    {
        var total = slices.Sum(s => s.Value);
        var dominant = slices.OrderByDescending(s => s.Value).First();
        var share = dominant.Value / (double)total;
        return $"{share:P0} of the games you played were {dominant.Label}.";
    }

    private void Log(EraAnalytics era, string decision) =>
        Console.WriteLine(
            $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: {decision}");
}