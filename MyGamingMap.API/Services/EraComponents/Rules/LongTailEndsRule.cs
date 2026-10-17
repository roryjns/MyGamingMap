namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public sealed class LongTailEndsRule : IEraComponentRule
{
    public string DataSource => "long-tail-ends";

    private const int MaxEntries = 4;

    public bool IsEligible(EraAnalytics era)
    {
        var count = era.LongTailEnds.Count;

        if (count == 0)
        {
            Console.WriteLine(
                $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: " +
                $"count=0 -> ineligible (no long-tail ends)");
            return false;
        }

        Console.WriteLine(
            $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: " +
            $"count={count} -> callout");

        return true;
    }

    public EraComponent Build(EraAnalytics era)
    {
        var entries = era.LongTailEnds
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
            Basis = ComponentBasis.LongTailEnds,  // placeholder
            Description = "Long-running games that ended this era.",
            Entries = entries,
        };
    }
}