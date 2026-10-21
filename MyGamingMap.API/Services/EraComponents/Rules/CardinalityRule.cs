namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public abstract class CardinalityRule<T> : IEraComponentRule
    where T : CategoryAnalytic
{
    protected abstract string EntityKey { get; }

    protected abstract IReadOnlyList<T> Entities(EraAnalytics era);
    protected abstract EnrichedPlayerGame? RepresentativeGame(EraAnalytics era, T entity);
    protected abstract ComponentBasis GetBasis(RankingMetric metric);
    protected abstract string DescriptionFor(RankingMetric metric);

    /// Stat shown under each entry in the callout. "412 hours", "3 games", etc.
    protected abstract string CalloutStatLabel(EraAnalytics era, T entity);

    private const int CalloutMax = 4;
    private const int CountdownMax = 10;
    private const double MinScore = 0.15;

    public string DataSource => $"cardinality:{EntityKey}";

    public bool IsEligible(EraAnalytics era)
    {
        var all = Entities(era);

        if (all.Count == 0)
        {
            Log(era, 0, "ineligible (no entries)");
            return false;
        }

        if (all.Count <= CalloutMax)
        {
            Log(era, all.Count, "callout, unit=hours");
            return true;
        }

        var (hours, games) = Scores(all);
        var metric = hours >= games ? RankingMetric.Hours : RankingMetric.GamesPlayed;
        var unit = metric == RankingMetric.Hours ? "hours" : "games played";
        var best = Math.Max(hours, games);

        if (best < MinScore)
        {
            Log(era, all.Count, $"ineligible (best {best:F3} < {MinScore:F3})");
            return false;
        }

        Log(era, all.Count, $"countdown, unit={unit}");
        return true;
    }

    public EraComponent Build(EraAnalytics era)
    {
        var all = Entities(era);

        return all.Count <= CalloutMax
            ? BuildCallout(era, all)
            : BuildCountdown(era, all);
    }

    private CalloutComponent BuildCallout(EraAnalytics era, IReadOnlyList<T> all)
    {
        var ordered = all.OrderByDescending(e => e.HoursPlayed).ToList();

        var entries = ordered
            .Select(e => new CalloutEntry
            {
                Label = e.Name,
                Game = RepresentativeGame(era, e),
                Value = e.HoursPlayed,
                Unit = "hours",
            })
            .ToList();

        return new CalloutComponent
        {
            Basis = GetBasis(RankingMetric.Hours),
            Description = DescriptionFor(RankingMetric.Hours),
            Entries = entries,
        };
    }

    private CountdownComponent BuildCountdown(EraAnalytics era, IReadOnlyList<T> all)
    {
        var (hours, games) = Scores(all);
        var metric = hours >= games ? RankingMetric.Hours : RankingMetric.GamesPlayed;

        var key = metric == RankingMetric.Hours
            ? (Func<T, double>)(e => e.HoursPlayed)
            : e => e.GamesPlayed;

        var total = all.Sum(key);
        var unit = metric == RankingMetric.Hours ? "hours" : "games played";

        var entries = all
            .OrderByDescending(key)
            .Take(CountdownMax)
            .Select((e, idx) =>
            {
                var value = key(e);
                return new CountdownEntry
                {
                    Rank = idx + 1,
                    Label = e.Name,
                    Game = RepresentativeGame(era, e),
                    Value = value,
                    Unit = unit,
                    Share = total > 0 ? value / total : null,
                };
            })
            .ToList();

        return new CountdownComponent
        {
            Basis = GetBasis(metric),
            Description = DescriptionFor(metric),
            Entries = entries,
        };
    }

    private static (double Hours, double Games) Scores(IReadOnlyList<T> all) =>
        (IEraComponentRule.ScoreMetric(all, e => e.HoursPlayed),
         IEraComponentRule.ScoreMetric(all, e => e.GamesPlayed));

    private void Log(EraAnalytics era, int count, string decision)
    {
        Console.WriteLine(
            $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: " +
            $"count={count} -> {decision}");
    }

    public double Strength(EraAnalytics era)
    {
        var all = Entities(era);
        if (all.Count < 2) return 0;

        var (hours, games) = Scores(all);
        return Math.Max(hours, games);
    }
}