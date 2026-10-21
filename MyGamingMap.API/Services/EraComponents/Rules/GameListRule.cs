namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public abstract class GameListRule : IEraComponentRule
{
    protected abstract string EntityKey { get; }
    protected abstract IReadOnlyList<EnrichedPlayerGame> Games(EraAnalytics era);

    protected abstract ComponentBasis Basis { get; }
    protected abstract string DescriptionFor(bool grid);
    protected abstract (double? Value, string? Unit) CalloutStat(EnrichedPlayerGame g);
    protected abstract GridEntry ToGridEntry(EnrichedPlayerGame g);

    private const int CalloutMax = 4;
    private const int GridMax = 30;
    protected virtual int GridStrengthMax => GridMax;

    public string DataSource => $"gamelist:{EntityKey}";
    
    public double Strength(EraAnalytics era) =>
        IEraComponentRule.CountStrength(Games(era).Count, GridStrengthMax);

    public bool IsEligible(EraAnalytics era)
    {
        var all = Games(era);

        if (all.Count == 0)
        {
            Log(era, 0, "ineligible (no games)");
            return false;
        }

        Log(era, all.Count, all.Count <= CalloutMax ? "callout" : "grid");
        return true;
    }

    public EraComponent Build(EraAnalytics era)
    {
        var all = Games(era);
        return all.Count <= CalloutMax
            ? BuildCallout(all)
            : BuildGrid(all);
    }

    private CalloutComponent BuildCallout(IReadOnlyList<EnrichedPlayerGame> all) =>
        new()
        {
            Basis = Basis,
            Description = DescriptionFor(grid: false),
            Entries = [.. all
                .Select(g =>
                {
                    var (value, unit) = CalloutStat(g);
                    return new CalloutEntry
                    {
                        Label = g.PlayerGame.Name,
                        Game = g,
                        Value = value,
                        Unit = unit,
                    };
                })
            ]
        };

    private GridComponent BuildGrid(IReadOnlyList<EnrichedPlayerGame> all)
    {
        return new()
        {
            Basis = Basis,
            Description = DescriptionFor(grid: true),
            Entries = [.. all.Select(ToGridEntry).Take(GridMax)],
        };
    }

    private void Log(EraAnalytics era, int count, string decision) =>
        Console.WriteLine(
            $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: " +
            $"count={count} -> {decision}");
}