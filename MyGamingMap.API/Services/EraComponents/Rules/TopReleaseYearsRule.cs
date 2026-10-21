namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public sealed class TopReleaseYearsRule : IEraComponentRule
{
    private const double MinScore = 0.2;
    private const int MinNonZeroYears = 2;

    public string DataSource => "bar:release-years";

    public double Strength(EraAnalytics era)
    {
        var ordered = BuildOrdered(era);
        var nonZero = ordered.Where(o => o.Value > 0).ToList();
        return IEraComponentRule.ScoreMetric(nonZero, o => o.Value);
    }

    public bool IsEligible(EraAnalytics era)
    {
        var ordered = BuildOrdered(era);

        if (ordered.Count == 0)
        {
            Log(era, "ineligible (no release years)");
            return false;
        }

        var nonZero = ordered.Where(o => o.Value > 0).ToList();
        if (nonZero.Count < MinNonZeroYears)
        {
            Log(era, $"ineligible (< {MinNonZeroYears} distinct years)");
            return false;
        }

        var score = IEraComponentRule.ScoreMetric(nonZero, o => o.Value);
        if (score < MinScore)
        {
            Log(era, $"ineligible (score {score:F3} < {MinScore:F3})");
            return false;
        }

        Log(era, $"bar (score={score:F3}, {ordered.Count} years)");
        return true;
    }

    public EraComponent Build(EraAnalytics era)
    {
        var ordered = BuildOrdered(era);

        return new BarChartComponent
        {
            Basis = ComponentBasis.ReleaseYearsByGamesPlayed,
            Description = Describe(ordered),
            Entries = [.. ordered
                .Select(o => new BarChartEntry
                {
                    Label = o.Label,
                    Value = o.Value,
                    Unit = "games",
                })],
        };
    }

    private static List<(string Label, int Value)> BuildOrdered(EraAnalytics era)
    {
        var analytics = era.IGDBAnalytics?.ReleaseDateAnalytics?.ReleaseYearAnalytics;
        if (analytics is null || analytics.Count == 0)
            return [];

        // Collapse any duplicate-year entries, drop unusable ones.
        var counts = analytics
            .Where(a => a.Name is not null && int.TryParse(a.Name, out _))
            .GroupBy(a => int.Parse(a.Name!))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.GamesPlayed));

        if (counts.Count == 0)
            return [];

        var min = counts.Keys.Min();
        var max = counts.Keys.Max();

        // Every year between lowest and highest, inclusive.
        return [.. Enumerable.Range(min, max - min + 1)
            .Select(year => (
                Label: year.ToString(),
                Value: counts.GetValueOrDefault(year)))];
    }

    private static string Describe(List<(string Label, int Value)> ordered)
    {
        var (Label, Value) = ordered.OrderByDescending(o => o.Value).First();
        return $"Most of the games you played came out in {Label}.";
    }

    private void Log(EraAnalytics era, string decision) =>
        Console.WriteLine(
            $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: {decision}");
}