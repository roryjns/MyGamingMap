namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public abstract class BarChartRule : IEraComponentRule
{
    protected abstract string EntityKey { get; }
    protected abstract IReadOnlyList<(string Label, int Value)> Bars(EraAnalytics era);
    protected abstract ComponentBasis Basis { get; }
    protected abstract string DescriptionFor(IReadOnlyList<(string Label, int Value)> bars);

    private const int MinEntries = 6;
    private const int MaxEntries = 10;
    private const double OtherThreshold = 0.05;

    public string DataSource => $"bar:{EntityKey}";

    public double Strength(EraAnalytics era) =>
        IEraComponentRule.MergedChartStrength(Bars(era), b => b);

    public bool IsEligible(EraAnalytics era)
    {
        var bars = Merge(Bars(era));
        if (bars.Count < MinEntries)
        {
            Log(era, bars.Count, $"ineligible (< {MinEntries} bars)");
            return false;
        }
        Log(era, bars.Count, "bar");
        return true;
    }

    public EraComponent Build(EraAnalytics era)
    {
        var bars = Merge(Bars(era));
        return new BarChartComponent
        {
            Basis = Basis,
            Description = DescriptionFor(bars),
            Entries = [.. bars
                .OrderByDescending(b => b.Value)
                .Take(MaxEntries)
                .Select(b => new BarChartEntry
                {
                    Label = b.Label, Value = b.Value, Unit = "games",
                })],
        };
    }

    // Same merge algorithm as ChartMerge but with this rule's thresholds.
    // Could be replaced by ChartMerge.Merge(Bars(era), OtherThreshold, MaxEntries).
    private static IReadOnlyList<(string Label, int Value)> Merge(
        IReadOnlyList<(string Label, int Value)> bars) =>
        ChartMerge.Merge(bars, OtherThreshold, MaxEntries);

    private void Log(EraAnalytics era, int count, string decision) =>
        Console.WriteLine(
            $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: " +
            $"count={count} -> {decision}");
}