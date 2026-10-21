namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public sealed class LongTailStartsRule : IEraComponentRule
{
    public string DataSource => "long-tail-starts";

    private const int MaxEntries = 4;

    public double Strength(EraAnalytics era) =>
    IEraComponentRule.CountStrength(era.LongTailStarts.Count, MaxEntries);

    public bool IsEligible(EraAnalytics era)
    {
        var count = era.LongTailStarts.Count;

        if (count == 0)
        {
            Console.WriteLine(
                $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: " +
                $"count=0 -> ineligible (no long-tail starts)");
            return false;
        }

        Console.WriteLine(
            $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: " +
            $"count={count} -> callout");

        return true;
    }

    public EraComponent Build(EraAnalytics era)
    {
        var entries = era.LongTailStarts
            .Take(MaxEntries)
            .Select(g => new CalloutEntry
            {
                Label = g.PlayerGame.Name,
                Game = g,
                Value = IEraComponentRule.SpanDays(g),
                Unit = "days",
            })
            .ToList();

        return new CalloutComponent
        {
            Basis = ComponentBasis.LongTailStarts,  // placeholder — see note below
            Description = "Long-running games that began this era.",
            Entries = entries,
        };
    }
}