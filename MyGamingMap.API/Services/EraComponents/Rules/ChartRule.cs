namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public abstract class ChartRule : IEraComponentRule
{
    protected abstract string EntityKey { get; }
    protected abstract IReadOnlyList<(string Label, int Value)> Slices(EraAnalytics era);
    protected abstract ComponentBasis Basis { get; }
    protected abstract string DescriptionFor(IReadOnlyList<(string Label, int Value)> slices);
    protected abstract string Unit { get; }

    private const int MinEntries = 2;
    private const int DonutMaxSlices = 5;

    public string DataSource => $"chart:{EntityKey}";

    public double Strength(EraAnalytics era) =>
        IEraComponentRule.MergedChartStrength(Slices(era), s => s);

    public bool IsEligible(EraAnalytics era)
    {
        var slices = ChartMerge.Merge(Slices(era));
        if (slices.Count < MinEntries)
        {
            Log(era, slices.Count, $"ineligible (< {MinEntries})");
            return false;
        }
        Log(era, slices.Count, slices.Count <= DonutMaxSlices ? "donut" : "bar");
        return true;
    }

    public EraComponent Build(EraAnalytics era)
    {
        var slices = ChartMerge.Merge(Slices(era));
        return slices.Count <= DonutMaxSlices ? BuildDonut(slices) : BuildBar(slices);
    }

    private DonutChartComponent BuildDonut(IReadOnlyList<(string Label, int Value)> slices) =>
        new()
        {
            Basis = Basis,
            Description = DescriptionFor(slices),
            Entries = [.. slices.Select(s => new DonutChartEntry
            {
                Label = s.Label, Value = s.Value, Unit = Unit,
            })],
        };

    private BarChartComponent BuildBar(IReadOnlyList<(string Label, int Value)> slices) =>
        new()
        {
            Basis = Basis,
            Description = DescriptionFor(slices),
            Entries = [.. slices.Select(s => new BarChartEntry
            {
                Label = s.Label, Value = s.Value, Unit = Unit,
            })],
        };

    private void Log(EraAnalytics era, int count, string decision) =>
        Console.WriteLine(
            $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: " +
            $"count={count} -> {decision}");
}

internal static class ChartMerge
{
    public static IReadOnlyList<(string Label, int Value)> Merge(
        IReadOnlyList<(string Label, int Value)> entries,
        double otherThreshold = 0.03, // Values that make up <= 3% of total get merged
        int maxNamed = 10)
    {
        var total = entries.Sum(e => e.Value);
        if (total <= 0) return entries;

        var ordered = entries.OrderByDescending(e => e.Value).ToList();

        var kept = new List<(string Label, int Value)>();
        var otherTotal = 0;
        var otherCount = 0;

        foreach (var e in ordered)
        {
            if (kept.Count < maxNamed && e.Value / (double)total >= otherThreshold)
                kept.Add(e);
            else
            {
                otherTotal += e.Value;
                otherCount++;
            }
        }

        if (otherCount > 0)
            kept.Add(("Other", otherTotal));

        return kept;
    }
}