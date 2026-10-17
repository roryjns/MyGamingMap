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
                    Label = b.Label,
                    Value = b.Value,
                    Unit = "games",
                })],
        };
    }

    private static IReadOnlyList<(string Label, int Value)> Merge(IReadOnlyList<(string Label, int Value)> bars)
    {
        var total = bars.Sum(b => b.Value);
        if (total <= 0) return bars;

        var ordered = bars.OrderByDescending(b => b.Value).ToList();

        var kept = new List<(string Label, int Value)>();
        var otherTotal = 0;
        var otherCount = 0;

        foreach (var b in ordered)
        {
            if (kept.Count < MaxEntries && b.Value / (double)total >= OtherThreshold)
                kept.Add(b);
            else
            {
                otherTotal += b.Value;
                otherCount++;
            }
        }

        if (otherCount > 0)
            kept.Add(("Other", otherTotal));

        return kept;
    }

    private void Log(EraAnalytics era, int count, string decision) =>
        Console.WriteLine(
            $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: " +
            $"count={count} -> {decision}");
}